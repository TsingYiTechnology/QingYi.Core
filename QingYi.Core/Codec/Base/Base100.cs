using System;
using System.Text;

namespace QingYi.Core.Codec.Base
{
    public class Base100
    {
        /// <summary>
        /// 获取Base100编码使用的完整字符集字符串（256个Emoji字符）
        /// </summary>
        public static string CharacterSet => GenerateCharacterSet();

        /// <summary>
        /// 重写ToString返回Base100字符集
        /// </summary>
        public override string ToString() => CharacterSet;

        #region 编码方法
        public static string Encode(byte[] data) => EncodeBytes(data);

        public static string Encode(string text, StringEncoding encoding = StringEncoding.UTF8)
        {
            byte[] bytes = GetEncoding(encoding).GetBytes(text);
            return EncodeBytes(bytes);
        }
        #endregion

        #region 解码方法
        public static byte[] Decode(string base100Text) => DecodeToBytes(base100Text);

        public static string Decode(string base100Text, StringEncoding encoding)
        {
            byte[] bytes = DecodeToBytes(base100Text);
            return GetEncoding(encoding).GetString(bytes);
        }
        #endregion

        #region 核心编解码实现
        private static unsafe string EncodeBytes(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) return string.Empty;

            int charCount = data.Length * 2;
            char[] buffer = new char[charCount];

            fixed (byte* pData = data)
            fixed (char* pBuffer = buffer)
            {
                byte* src = pData;
                char* dest = pBuffer;

                for (int i = 0; i < data.Length; i++)
                {
                    byte b = *src++;
                    *dest++ = (char)0xD83C;        // 高位代理固定值
                    *dest++ = (char)(0xDF00 + b);  // 低位代理计算
                }
            }

            return new string(buffer);
        }

        private static unsafe byte[] DecodeToBytes(string base100Text)
        {
            if (base100Text == null) throw new ArgumentNullException(nameof(base100Text));
            if (base100Text.Length == 0) return Array.Empty<byte>();
            if (base100Text.Length % 2 != 0)
                throw new ArgumentException("Base100文本长度必须是偶数", nameof(base100Text));

            int byteCount = base100Text.Length / 2;
            byte[] result = new byte[byteCount];

            fixed (char* pText = base100Text)
            fixed (byte* pResult = result)
            {
                char* src = pText;
                byte* dest = pResult;

                for (int i = 0; i < byteCount; i++)
                {
                    char high = *src++;
                    char low = *src++;

                    if (high != 0xD83C || low < 0xDF00 || low > 0xDFFF)
                        throw new FormatException($"无效的Base100字符对位置: {i * 2}");

                    *dest++ = (byte)(low - 0xDF00);
                }
            }

            return result;
        }
        #endregion

        #region 编码转换辅助
        private static Encoding GetEncoding(StringEncoding encoding)
        {
            switch (encoding)
            {
                case StringEncoding.UTF8:
                    return Encoding.UTF8;
                case StringEncoding.UTF16LE:
                    return Encoding.Unicode;
                case StringEncoding.UTF16BE:
                    return Encoding.BigEndianUnicode;
                case StringEncoding.ASCII:
                    return Encoding.ASCII;
                case StringEncoding.UTF32:
                    return Encoding.UTF32;
#if NET6_0_OR_GREATER
                case StringEncoding.Latin1:
                    return Encoding.Latin1;
#endif
#pragma warning disable SYSLIB0001, CS0618
                case StringEncoding.UTF7:
                    return Encoding.UTF7;
#pragma warning restore SYSLIB0001, CS0618
                default:
                    throw new NotSupportedException($"不支持的编码: {encoding}");
            }
        }
        #endregion

        #region 字符集生成
        private static unsafe string GenerateCharacterSet()
        {
            const int charCount = 256 * 2;
            char[] chars = new char[charCount];

            fixed (char* pChars = chars)
            {
                char* ptr = pChars;
                for (int i = 0; i < 256; i++)
                {
                    *ptr++ = (char)0xD83C;
                    *ptr++ = (char)(0xDF00 + i);
                }
            }

            return new string(chars);
        }
        #endregion
    }
}
