#if NET5_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Text;

namespace QingYi.Core.Codec.Base
{
    public class Base94
    {
        /// <summary>
        /// Base94 字符集（33-126 的 ASCII 字符）
        /// </summary>
        private const string Base94Chars = "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

        /// <summary>
        /// 字符到索引的查找表
        /// </summary>
        private static readonly sbyte[] CharToValueMap = new sbyte[128];

        /// <summary>
        /// 静态构造函数初始化查找表
        /// </summary>
        static Base94()
        {
            // 初始化为-1表示无效字符
            Array.Fill(CharToValueMap, (sbyte)-1);

            // 填充有效字符映射
            for (int i = 0; i < Base94Chars.Length; i++)
            {
                char c = Base94Chars[i];
                if (c < 128) CharToValueMap[c] = (sbyte)i;
            }
        }

        /// <summary>
        /// 获取指定编码的 Encoding 实例
        /// </summary>
        private static Encoding GetEncoding(StringEncoding encoding)
        {
            switch (encoding)
            {
                case StringEncoding.UTF8: return Encoding.UTF8;
                case StringEncoding.UTF16LE: return Encoding.Unicode;
                case StringEncoding.UTF16BE: return Encoding.BigEndianUnicode;
                case StringEncoding.ASCII: return Encoding.ASCII;
                case StringEncoding.UTF32: return Encoding.UTF32;
#if NET6_0_OR_GREATER
                case StringEncoding.Latin1: return Encoding.Latin1;
#endif
#pragma warning disable SYSLIB0001, CS0618
                case StringEncoding.UTF7:
                    return Encoding.UTF7;
#pragma warning restore SYSLIB0001, CS0618
                default: return Encoding.UTF8;
            }
        }

        /// <summary>
        /// 将字节数组编码为 Base94 字符串
        /// </summary>
        public static unsafe string Encode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) return string.Empty;

            // 计算输出长度（每3字节转4字符）
            int outputLength = (int)Math.Ceiling(data.Length / 3.0) * 4;
            return string.Create(outputLength, data, (chars, state) =>
            {
                fixed (byte* pData = state)
                fixed (char* pChars = chars)
                {
                    byte* input = pData;
                    char* output = pChars;
                    int remaining = state.Length;

                    while (remaining >= 3)
                    {
                        uint value = (uint)(*input++) << 16;
                        value |= (uint)(*input++) << 8;
                        value |= *input++;
                        remaining -= 3;

                        *output++ = Base94Chars[(int)(value / 830584)];   // 94^3
                        *output++ = Base94Chars[(int)(value / 8836) % 94]; // 94^2
                        *output++ = Base94Chars[(int)(value / 94) % 94];
                        *output++ = Base94Chars[(int)(value % 94)];
                    }

                    // 处理剩余字节（1或2个）
                    if (remaining > 0)
                    {
                        uint value = 0;
                        if (remaining == 1)
                        {
                            value = (uint)(*input++) << 16;
                            *output++ = Base94Chars[(int)(value / 830584)];
                            *output++ = Base94Chars[(int)(value / 8836) % 94];
                            *output++ = '=';
                            *output++ = '=';
                        }
                        else // remaining == 2
                        {
                            value = (uint)(*input++) << 16;
                            value |= (uint)(*input++) << 8;
                            *output++ = Base94Chars[(int)(value / 830584)];
                            *output++ = Base94Chars[(int)(value / 8836) % 94];
                            *output++ = Base94Chars[(int)(value / 94) % 94];
                            *output++ = '=';
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 将字符串编码为 Base94 字符串
        /// </summary>
        public static string Encode(string text, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            byte[] data = GetEncoding(encoding).GetBytes(text);
            return Encode(data);
        }

        /// <summary>
        /// 将 Base94 字符串解码为字节数组
        /// </summary>
        public static unsafe byte[] Decode(string base94)
        {
            if (base94 == null) throw new ArgumentNullException(nameof(base94));
            if (base94.Length == 0) return Array.Empty<byte>();
            if (base94.Length % 4 != 0) throw new ArgumentException("Base94 string length must be multiple of 4");

            // 计算输出长度（处理填充）
            int groupCount = base94.Length / 4;
            int padding = base94[^1] == '=' ? (base94[^2] == '=' ? 2 : 1) : 0;
            int outputLength = groupCount * 3 - padding;
            byte[] result = new byte[outputLength];

            fixed (char* pBase94 = base94)
            fixed (byte* pResult = result)
            {
                char* input = pBase94;
                byte* output = pResult;

                for (int i = 0; i < groupCount; i++)
                {
                    uint value = 0;
                    int validChars = 4;

                    // 处理组内字符
                    for (int j = 0; j < 4; j++)
                    {
                        char c = *input++;
                        if (c == '=')
                        {
                            if (j < 2) throw new ArgumentException("Invalid padding position");
                            validChars = j;
                            break;
                        }

                        // 验证字符范围
                        if (c >= 128 || CharToValueMap[c] == -1)
                            throw new ArgumentException($"Invalid character: '{c}' (0x{(int)c:X4})");

                        value = value * 94 + (uint)CharToValueMap[c];
                    }

                    // 根据有效字符数输出字节
                    if (validChars >= 2) *output++ = (byte)(value >> 16);
                    if (validChars >= 3) *output++ = (byte)(value >> 8);
                    if (validChars >= 4) *output++ = (byte)value;
                }
            }

            return result;
        }

        /// <summary>
        /// 将 Base94 字符串解码为字符串
        /// </summary>
        public static string DecodeToString(string base94, StringEncoding encoding = StringEncoding.UTF8)
        {
            byte[] data = Decode(base94);
            return GetEncoding(encoding).GetString(data);
        }

        /// <summary>
        /// 返回 Base94 字符集
        /// </summary>
        public override string ToString() => Base94Chars;
    }
}
#endif
