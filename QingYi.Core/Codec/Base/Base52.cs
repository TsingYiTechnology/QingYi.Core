#if NET5_0_OR_GREATER
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace QingYi.Core.Codec.Base
{
    public class Base52
    {
        /// <summary>
        /// Base52字符集（26大写字母+26小写字母）
        /// </summary>
        public const string CharacterSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        /// <summary>
        /// 填充字符
        /// </summary>
        public const char PaddingChar = '=';
        private const int BlockSize = 5; // 编码后每组字符数
        private const int ByteBlockSize = 3; // 原始字节每组大小
        private static readonly byte[] CharToIndexMap = new byte[128];
        private static readonly bool CharMapInitialized = InitializeCharMap();

        private static bool InitializeCharMap()
        {
            for (int i = 0; i < CharToIndexMap.Length; i++)
                CharToIndexMap[i] = 0xFF; // 标记无效字符

            for (byte i = 0; i < CharacterSet.Length; i++)
                CharToIndexMap[CharacterSet[i]] = i;

            return true;
        }

        /// <summary>
        /// 重写ToString返回字符集
        /// </summary>
        public override string ToString() => CharacterSet;

        /// <summary>
        /// 编码字节数组
        /// </summary>
        public unsafe string Encode(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            int length = data.Length;
            int fullBlocks = length / ByteBlockSize;
            int remainder = length % ByteBlockSize;

            // 计算输出长度: (n/3)*5 + (余数? 2/3/0) + 填充
            int outputLength = fullBlocks * BlockSize;
            switch (remainder)
            {
                case 1: outputLength += 2; break;
                case 2: outputLength += 3; break;
            }
            int padding = (BlockSize - (outputLength % BlockSize)) % BlockSize;
            outputLength += padding;

            char[] output = new char[outputLength];
            fixed (byte* inputPtr = data)
            fixed (char* outputPtr = output)
            fixed (char* charSetPtr = CharacterSet)
            {
                char* op = outputPtr;
                byte* ip = inputPtr;

                // 处理完整3字节块
                for (int i = 0; i < fullBlocks; i++, ip += 3)
                {
                    uint value = (uint)(*ip << 16) | (uint)(ip[1] << 8) | ip[2];
                    *op++ = charSetPtr[value / 7311616];          // 52^4
                    *op++ = charSetPtr[(value / 140608) % 52];    // 52^3
                    *op++ = charSetPtr[(value / 2704) % 52];      // 52^2
                    *op++ = charSetPtr[(value / 52) % 52];
                    *op++ = charSetPtr[value % 52];
                }

                // 处理剩余字节
                if (remainder > 0)
                {
                    uint value = (uint)(*ip << 16);
                    if (remainder == 2) value |= (uint)(ip[1] << 8);

                    *op++ = charSetPtr[value / 7311616];
                    *op++ = charSetPtr[(value / 140608) % 52];

                    if (remainder == 2)
                    {
                        *op++ = charSetPtr[(value / 2704) % 52];
                    }
                }

                // 填充
                for (int i = 0; i < padding; i++)
                    outputPtr[outputLength - 1 - i] = PaddingChar;
            }

            return new string(output);
        }

        /// <summary>
        /// 解码Base52字符串
        /// </summary>
        public unsafe byte[] Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return Array.Empty<byte>();

            // 验证长度
            if (encoded.Length % BlockSize != 0)
                throw new FormatException("Base52字符串长度必须是5的倍数");

            int length = encoded.Length;
            int padding = CalculatePadding(encoded, length);
            int fullBlocks = (length - BlockSize) / BlockSize;
            int lastBlockLength = BlockSize - padding;
            int outputLength = fullBlocks * ByteBlockSize + GetOutputSize(lastBlockLength);

            byte[] output = new byte[outputLength];
            fixed (char* inputPtr = encoded)
            fixed (byte* outputPtr = output)
            {
                byte* op = outputPtr;
                char* ip = inputPtr;

                // 处理完整块
                for (int i = 0; i < fullBlocks; i++, ip += BlockSize)
                {
                    uint value = 0;
                    for (int j = 0; j < BlockSize; j++)
                    {
                        value *= 52;
                        value += GetCharValue(ip[j]);
                    }

                    *op++ = (byte)(value >> 16);
                    *op++ = (byte)(value >> 8);
                    *op++ = (byte)value;
                }

                // 处理最后一块
                uint lastValue = 0;
                for (int j = 0; j < lastBlockLength; j++)
                {
                    lastValue *= 52;
                    lastValue += GetCharValue(ip[j]);
                }

                switch (lastBlockLength)
                {
                    case 5: // 3字节
                        *op++ = (byte)(lastValue >> 16);
                        *op++ = (byte)(lastValue >> 8);
                        *op++ = (byte)lastValue;
                        break;

                    case 3: // 2字节
                        *op++ = (byte)(lastValue >> 8);
                        *op++ = (byte)lastValue;
                        break;

                    case 2: // 1字节
                        *op++ = (byte)lastValue;
                        break;
                }
            }

            return output;
        }

        #region 字符串编解码重载
        public string Encode(string text, StringEncoding encoding = StringEncoding.UTF8)
            => Encode(GetBytes(text, encoding));

        public string Decode(string base52, StringEncoding encoding = StringEncoding.UTF8)
            => GetString(Decode(base52), encoding);

        public static string EncodeText(string text, StringEncoding encoding = StringEncoding.UTF8)
            => new Base52().Encode(text, encoding);

        public static string DecodeText(string base52, StringEncoding encoding = StringEncoding.UTF8)
            => new Base52().Decode(base52, encoding);
        #endregion

        #region 私有辅助方法
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculatePadding(string encoded, int length)
        {
            int padding = 0;
            for (int i = length - 1; i >= 0 && encoded[i] == PaddingChar; i--)
                padding++;

            if (padding != 0 && padding != 2 && padding != 3)
                throw new FormatException("无效的填充格式");

            return padding;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetOutputSize(int charsInBlock)
            => charsInBlock switch
            {
                5 => 3, // 3字节
                3 => 2, // 2字节
                2 => 1, // 1字节
                _ => throw new FormatException("无效的字符块大小")
            };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetCharValue(char c)
        {
            if (c >= CharToIndexMap.Length || CharToIndexMap[c] == 0xFF)
                throw new FormatException($"无效字符: '{c}'");
            return CharToIndexMap[c];
        }

        private static byte[] GetBytes(string text, StringEncoding encoding)
        {
            return encoding switch
            {
                StringEncoding.UTF8 => Encoding.UTF8.GetBytes(text),
                StringEncoding.UTF16LE => Encoding.Unicode.GetBytes(text),
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetBytes(text),
                StringEncoding.ASCII => Encoding.ASCII.GetBytes(text),
                StringEncoding.UTF32 => Encoding.UTF32.GetBytes(text),
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1.GetBytes(text),
#endif
                StringEncoding.UTF7 => GetUTF7Bytes(text),
                _ => throw new ArgumentOutOfRangeException(nameof(encoding))
            };
        }

        private static string GetString(byte[] data, StringEncoding encoding)
        {
            return encoding switch
            {
                StringEncoding.UTF8 => Encoding.UTF8.GetString(data),
                StringEncoding.UTF16LE => Encoding.Unicode.GetString(data),
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetString(data),
                StringEncoding.ASCII => Encoding.ASCII.GetString(data),
                StringEncoding.UTF32 => Encoding.UTF32.GetString(data),
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1.GetString(data),
#endif
                StringEncoding.UTF7 => GetUTF7String(data),
                _ => throw new ArgumentOutOfRangeException(nameof(encoding))
            };
        }

#pragma warning disable SYSLIB0001, CS0618
        private static byte[] GetUTF7Bytes(string text) => Encoding.UTF7.GetBytes(text);
        private static string GetUTF7String(byte[] data) => Encoding.UTF7.GetString(data);
#pragma warning restore SYSLIB0001, CS0618
        #endregion
    }
}
#endif
