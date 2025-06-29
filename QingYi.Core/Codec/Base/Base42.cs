#if NET5_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace QingYi.Core.Codec.Base
{
    public class Base42
    {
        // Base42字符集 (42个字符)
        private const string CHARSET = "1234567890!@#$%^&*()~`_+-={}|[]\\:\";'<>?,./";
        private const char PADDING_CHAR = 'A';
        private const int GROUP_BYTES = 3;
        private const int GROUP_CHARS = 5;
        private static readonly byte[] s_decodeMap = new byte[128];
        private static readonly uint[] s_powers = { 1, 42, 1764, 74088, 3111696 };

        static Base42()
        {
            // 初始化解码映射表
            Array.Fill(s_decodeMap, byte.MaxValue);
            for (byte i = 0; i < CHARSET.Length; i++)
            {
                char c = CHARSET[i];
                s_decodeMap[c] = i;
            }
        }

        /// <summary>
        /// 返回Base42字符集
        /// </summary>
        public override string ToString() => CHARSET;

        #region 编码方法
        public static string Encode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) return string.Empty;

            int groups = (data.Length + GROUP_BYTES - 1) / GROUP_BYTES;
            char[] result = new char[groups * GROUP_CHARS];

            unsafe
            {
                fixed (byte* dataPtr = data)
                fixed (char* resultPtr = result)
                {
                    int dataIndex = 0;
                    char* current = resultPtr;
                    int remaining = data.Length;

                    while (remaining >= GROUP_BYTES)
                    {
                        EncodeGroup(dataPtr + dataIndex, current);
                        dataIndex += GROUP_BYTES;
                        current += GROUP_CHARS;
                        remaining -= GROUP_BYTES;
                    }

                    if (remaining > 0)
                    {
                        EncodeLastGroup(dataPtr + dataIndex, current, remaining);
                    }
                }
            }

            return new string(result);
        }

        public static string EncodeString(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (input == null) return null;
            byte[] data = GetBytes(input, encoding);
            return Encode(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void EncodeGroup(byte* data, char* output)
        {
            uint value = (uint)data[0] << 16 | (uint)data[1] << 8 | data[2];
            for (int i = GROUP_CHARS - 1; i >= 0; i--)
            {
                output[i] = CHARSET[(int)(value % 42)];
                value /= 42;
            }
        }

        private static unsafe void EncodeLastGroup(byte* data, char* output, int count)
        {
            Debug.Assert(count > 0 && count < GROUP_BYTES);

            uint value = 0;
            for (int i = 0; i < count; i++)
            {
                value = (value << 8) | data[i];
            }

            int charsNeeded = count switch
            {
                1 => 2,
                2 => 3,
                _ => throw new InvalidOperationException()
            };

            for (int i = charsNeeded - 1; i >= 0; i--)
            {
                output[i] = CHARSET[(int)(value % 42)];
                value /= 42;
            }

            // 填充剩余字符
            for (int i = charsNeeded; i < GROUP_CHARS; i++)
            {
                output[i] = PADDING_CHAR;
            }
        }
        #endregion

        #region 解码方法
        public static byte[] Decode(string base42)
        {
            if (base42 == null) throw new ArgumentNullException(nameof(base42));
            if (base42.Length == 0) return Array.Empty<byte>();

            if (base42.Length % GROUP_CHARS != 0)
                throw new ArgumentException($"Base42 string length must be multiple of {GROUP_CHARS}");

            int totalGroups = base42.Length / GROUP_CHARS;
            List<byte> result = new List<byte>(totalGroups * GROUP_BYTES);

            unsafe
            {
                fixed (char* base42Ptr = base42)
                {
                    for (int groupIndex = 0; groupIndex < totalGroups; groupIndex++)
                    {
                        char* groupStart = base42Ptr + groupIndex * GROUP_CHARS;
                        DecodeGroup(groupStart, result, groupIndex == totalGroups - 1);
                    }
                }
            }

            return result.ToArray();
        }

        public static string DecodeString(string base42, StringEncoding encoding = StringEncoding.UTF8)
        {
            byte[] data = Decode(base42);
            return GetString(data, encoding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void DecodeGroup(char* input, List<byte> output, bool isLastGroup)
        {
            uint value = 0;
            int validChars = GROUP_CHARS;

            // 如果是最后一组，检查填充
            if (isLastGroup)
            {
                for (int i = GROUP_CHARS - 1; i >= 0; i--)
                {
                    if (input[i] == PADDING_CHAR) validChars--;
                    else break;
                }
            }

            // 解码有效字符
            for (int i = 0; i < validChars; i++)
            {
                char c = input[i];
                if (c >= s_decodeMap.Length || s_decodeMap[c] == byte.MaxValue)
                    throw new ArgumentException($"Invalid character '{c}' in Base42 string");

                value = value * 42 + s_decodeMap[c];
            }

            // 计算需要的字节数
            int bytesNeeded = validChars switch
            {
                2 => 1,
                3 => 2,
                5 => 3,
                _ => throw new ArgumentException("Invalid group size in Base42 string")
            };

            // 提取字节
            for (int i = bytesNeeded - 1; i >= 0; i--)
            {
                byte b = (byte)(value >> (8 * i));
                output.Add(b);
            }
        }
        #endregion

        #region 编码辅助方法
        private static byte[] GetBytes(string s, StringEncoding encoding)
        {
            return encoding switch
            {
                StringEncoding.UTF8 => Encoding.UTF8.GetBytes(s),
                StringEncoding.UTF16LE => Encoding.Unicode.GetBytes(s),
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetBytes(s),
                StringEncoding.ASCII => Encoding.ASCII.GetBytes(s),
                StringEncoding.UTF32 => Encoding.UTF32.GetBytes(s),
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1.GetBytes(s),
#endif
                StringEncoding.UTF7 => GetUTF7Bytes(s),
                _ => throw new ArgumentException("Unsupported encoding")
            };
        }

        private static string GetString(byte[] bytes, StringEncoding encoding)
        {
            return encoding switch
            {
                StringEncoding.UTF8 => Encoding.UTF8.GetString(bytes),
                StringEncoding.UTF16LE => Encoding.Unicode.GetString(bytes),
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetString(bytes),
                StringEncoding.ASCII => Encoding.ASCII.GetString(bytes),
                StringEncoding.UTF32 => Encoding.UTF32.GetString(bytes),
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1.GetString(bytes),
#endif
                StringEncoding.UTF7 => GetUTF7String(bytes),
                _ => throw new ArgumentException("Unsupported encoding")
            };
        }

#pragma warning disable SYSLIB0001, CS0618
        private static byte[] GetUTF7Bytes(string s) => Encoding.UTF7.GetBytes(s);
        private static string GetUTF7String(byte[] bytes) => Encoding.UTF7.GetString(bytes);
#pragma warning restore SYSLIB0001, CS0618
        #endregion
    }
}
#endif
