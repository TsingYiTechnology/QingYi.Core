using System;
using System.Text;

namespace QingYi.Core.Codec.Base
{
    public class Base92
    {
        /// <summary>
        /// Base92 字符集 (94 个可打印 ASCII 字符)
        /// </summary>
        private const string ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&()*+,./:;<=>?@[]^_`{|}~'";

        /// <summary>
        /// 字符映射表 (ASCII 值到 Base92 索引)
        /// </summary>
        private static readonly byte[] CHAR_MAP = new byte[256];

        static Base92()
        {
            // 初始化字符映射表
            for (int i = 0; i < CHAR_MAP.Length; i++)
            {
                CHAR_MAP[i] = 0xFF; // 0xFF 表示无效字符
            }

            // 填充有效字符映射
            for (byte idx = 0; idx < ALPHABET.Length; idx++)
            {
                char c = ALPHABET[idx];
                CHAR_MAP[c] = idx;
            }
        }

        /// <summary>
        /// 返回 Base92 字符集
        /// </summary>
        public override string ToString() => ALPHABET;

        #region 编码方法

        /// <summary>
        /// 将字节数组编码为 Base92 字符串
        /// </summary>
        public static string Encode(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            unsafe
            {
                int inLen = data.Length;
                // 最大输出长度估算: (输入字节数 * 8 / 6.5) + 2
                int maxOutLen = (int)Math.Ceiling(inLen * 8 / 6.5) + 2;
                char* output = stackalloc char[maxOutLen];
                char* outPtr = output;

                fixed (byte* inPtr = data)
                {
                    byte* inEnd = inPtr + inLen;
                    byte* inP = inPtr;

                    uint bitBuffer = 0;
                    int bitCount = 0;

                    while (inP < inEnd)
                    {
                        bitBuffer = (bitBuffer << 8) | *inP++;
                        bitCount += 8;

                        while (bitCount >= 13)
                        {
                            bitCount -= 13;
                            uint value = (bitBuffer >> bitCount) & 0x1FFF; // 提取13位

                            uint idx1 = value / 92;
                            uint idx2 = value % 92;
                            *outPtr++ = ALPHABET[(int)idx1];
                            *outPtr++ = ALPHABET[(int)idx2];
                        }
                    }

                    // 处理剩余位
                    if (bitCount > 0)
                    {
                        // 将剩余位移到13位高区
                        bitBuffer <<= (13 - bitCount);
                        uint value = bitBuffer & 0x1FFF;

                        // 根据值大小决定输出1或2个字符
                        if (bitCount > 7 || value >= 92)
                        {
                            uint idx1 = value / 92;
                            uint idx2 = value % 92;
                            *outPtr++ = ALPHABET[(int)idx1];
                            *outPtr++ = ALPHABET[(int)idx2];
                        }
                        else
                        {
                            *outPtr++ = ALPHABET[(int)value];
                        }
                    }
                }

                return new string(output, 0, (int)(outPtr - output));
            }
        }

        /// <summary>
        /// 将字符串编码为 Base92 字符串 (默认 UTF-8 编码)
        /// </summary>
        public static string Encode(string text, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            Encoding enc = GetEncoding(encoding);
            byte[] data = enc.GetBytes(text);
            return Encode(data);
        }

        #endregion

        #region 解码方法

        /// <summary>
        /// 将 Base92 字符串解码为字节数组
        /// </summary>
        public static byte[] Decode(string base92)
        {
            if (string.IsNullOrEmpty(base92))
                return Array.Empty<byte>();

            unsafe
            {
                int inLen = base92.Length;
                // 输出缓冲区最大长度: (输入字符数 * 13 + 7) / 8
                int maxOutLen = (inLen * 13 + 7) / 8;
                byte[] outputArray = new byte[maxOutLen];
                int actualOutLen = 0;

                fixed (char* inPtr = base92)
                fixed (byte* outPtr = outputArray)
                {
                    char* inEnd = inPtr + inLen;
                    char* inP = inPtr;
                    byte* outP = outPtr;

                    uint bitBuffer = 0;
                    int bitCount = 0;
                    bool lastBlockIsSingle = false;

                    // 处理完整字符对
                    while (inP < inEnd)
                    {
                        // 获取第一个字符的值
                        char c1 = *inP++;
                        byte v1 = CHAR_MAP[c1];
                        if (v1 == 0xFF)
                            throw new FormatException($"Invalid Base92 character: '{c1}' (0x{(byte)c1:X2})");

                        // 检查是否有第二个字符
                        if (inP >= inEnd)
                        {
                            // 最后一个字符是单个字符
                            lastBlockIsSingle = true;
                            bitBuffer = (bitBuffer << 7) | v1;
                            bitCount += 7;
                            break;
                        }

                        // 获取第二个字符的值
                        char c2 = *inP++;
                        byte v2 = CHAR_MAP[c2];
                        if (v2 == 0xFF)
                            throw new FormatException($"Invalid Base92 character: '{c2}' (0x{(byte)c2:X2})");

                        // 组合两个字符为13位值
                        uint value = (uint)(v1 * 92 + v2);
                        bitBuffer = (bitBuffer << 13) | value;
                        bitCount += 13;

                        // 提取完整字节
                        while (bitCount >= 8)
                        {
                            bitCount -= 8;
                            *outP++ = (byte)(bitBuffer >> bitCount);
                            actualOutLen++;
                        }
                    }

                    // 处理最后一个字符块（如果是单个字符）
                    if (lastBlockIsSingle)
                    {
                        // 提取剩余字节（最多1个）
                        if (bitCount >= 8)
                        {
                            bitCount -= 8;
                            *outP++ = (byte)(bitBuffer >> bitCount);
                            actualOutLen++;
                        }

                        // 检查剩余位
                        if (bitCount > 0)
                        {
                            uint mask = (1u << bitCount) - 1;
                            if ((bitBuffer & mask) != 0)
                            {
                                throw new FormatException(
                                    $"Invalid padding: {bitCount} extra bits with non-zero value (0x{bitBuffer & mask:X})");
                            }
                        }
                    }
                }

                // 返回实际长度的字节数组
                if (actualOutLen == outputArray.Length)
                    return outputArray;

                byte[] result = new byte[actualOutLen];
                Buffer.BlockCopy(outputArray, 0, result, 0, actualOutLen);
                return result;
            }
        }

        /// <summary>
        /// 将 Base92 字符串解码为字符串 (默认 UTF-8 编码)
        /// </summary>
        public static string DecodeToString(string base92, StringEncoding encoding = StringEncoding.UTF8)
        {
            byte[] data = Decode(base92);
            Encoding enc = GetEncoding(encoding);
            return enc.GetString(data);
        }

        #endregion

        #region 编码辅助方法

        /// <summary>
        /// 根据枚举获取对应的编码对象
        /// </summary>
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
                    throw new ArgumentException("Unsupported encoding", nameof(encoding));
            }
        }

        #endregion
    }
}
