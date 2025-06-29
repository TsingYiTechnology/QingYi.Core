#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Buffers;

namespace QingYi.Core.Codec.Base
{
    public enum Base85Variant
    {
        /// <summary>
        /// Standard Ascii85 (RFC 1924)
        /// </summary>
        Ascii85,

        /// <summary>
        /// ZeroMQ's Z85 variant (RFC 32)
        /// </summary>
        Z85,

        /// <summary>
        /// Git binary patch variant
        /// </summary>
        Git,

        /// <summary>
        /// IPv6 variant (RFC 1924)
        /// </summary>
        IPv6
    }

    public static class Base85
    {
        // 变体字符映射表
        private static readonly Dictionary<Base85Variant, char[]> VariantAlphabets = new()
        {
            [Base85Variant.Ascii85] = "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstu".ToCharArray(),
            [Base85Variant.Z85] = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#".ToCharArray(),
            [Base85Variant.Git] = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!#$%&()*+-;<=>?@^_`{|}~".ToCharArray(),
            [Base85Variant.IPv6] = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!#$%&()*+-;<=>?@^_`{|}~".ToCharArray()
        };

        // 变体特殊字符处理
        private static readonly Dictionary<Base85Variant, (char Zero, char Padding, string Prefix, string Suffix)> VariantOptions = new()
        {
            [Base85Variant.Ascii85] = ('z', '~', "<~", "~>"),
            [Base85Variant.Z85] = ('\0', '\0', "", ""),
            [Base85Variant.Git] = ('\0', '\0', "", ""),
            [Base85Variant.IPv6] = ('\0', '\0', "", "")
        };

        #region 编码方法
        public static unsafe string Encode(byte[] data, Base85Variant variant = Base85Variant.Ascii85)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (variant == Base85Variant.Z85 && data.Length % 4 != 0)
                throw new ArgumentException("Z85 requires input length to be multiple of 4");

            var options = VariantOptions[variant];
            var alphabet = VariantAlphabets[variant];
            var prefix = options.Prefix;
            var suffix = options.Suffix;
            var useZero = options.Zero != '\0';

            // 计算输出长度
            int blockCount = data.Length / 4;
            int remainingBytes = data.Length % 4;
            int outputLength = prefix.Length +
                              blockCount * 5 +
                              (remainingBytes > 0 ? 5 : 0) +
                              suffix.Length;

            // 使用数组池租用字符数组
            char[] outputBuffer = ArrayPool<char>.Shared.Rent(outputLength);
            int charPos = 0;

            try
            {
                // 添加前缀
                for (int i = 0; i < prefix.Length; i++)
                {
                    outputBuffer[charPos++] = prefix[i];
                }

                fixed (byte* pData = data)
                fixed (char* pOutput = outputBuffer)
                {
                    uint* pUint = (uint*)pData;
                    char* currentOut = pOutput + prefix.Length;

                    for (int i = 0; i < blockCount; i++)
                    {
                        if (useZero && pUint[i] == 0)
                        {
                            *currentOut++ = options.Zero;
                            charPos++;
                            continue;
                        }

                        // 转换为大端序后编码
                        uint block = BitConverter.IsLittleEndian ? ReverseBytes(pUint[i]) : pUint[i];
                        int count = EncodeBlock(block, currentOut, variant);
                        currentOut += count;
                        charPos += count;
                    }

                    if (remainingBytes > 0)
                    {
                        uint lastBlock = 0;
                        byte* pLast = (byte*)pData + blockCount * 4;
                        // 按大端序构建剩余块
                        for (int i = 0; i < remainingBytes; i++)
                        {
                            lastBlock = (lastBlock << 8) | pLast[i];
                        }
                        lastBlock <<= (32 - remainingBytes * 8);

                        // 转换为大端序后编码
                        if (BitConverter.IsLittleEndian)
                        {
                            lastBlock = ReverseBytes(lastBlock);
                        }
                        int count = EncodeBlock(lastBlock, currentOut, variant);
                        charPos += count;
                    }
                }

                // 添加后缀
                for (int i = 0; i < suffix.Length; i++)
                {
                    outputBuffer[charPos++] = suffix[i];
                }

                return new string(outputBuffer, 0, charPos);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(outputBuffer);
            }
        }

        public static string Encode(string text, StringEncoding encoding = StringEncoding.UTF8, Base85Variant variant = Base85Variant.Ascii85)
        {
            byte[] data = GetBytes(text, encoding);
            return Encode(data, variant);
        }
        #endregion

        #region 解码方法
        public static byte[] Decode(string base85, Base85Variant variant = Base85Variant.Ascii85)
        {
            if (base85 == null) throw new ArgumentNullException(nameof(base85));

            var options = VariantOptions[variant];
            var alphabet = VariantAlphabets[variant];
            var prefix = options.Prefix;
            var suffix = options.Suffix;

            // 处理前缀/后缀
            if (base85.StartsWith(prefix)) base85 = base85[prefix.Length..];
            if (base85.EndsWith(suffix)) base85 = base85[..^suffix.Length];

            // 清理无效字符
            base85 = Regex.Replace(base85, @"\s+", "");
            if (variant == Base85Variant.Z85 && base85.Length % 5 != 0)
                throw new FormatException("Z85 requires input length to be multiple of 5");

            // 检查无效长度（非Z85变体）
            if (variant != Base85Variant.Z85 && base85.Length % 5 == 1)
                throw new FormatException("Invalid Base85 string length: last block must not be 1 character");

            int blockCount = base85.Length / 5;
            int remainingChars = base85.Length % 5;
            int outputLength = blockCount * 4 + (remainingChars > 0 ? remainingChars - 1 : 0);
            var result = new byte[outputLength];

            unsafe
            {
                fixed (char* pBase85 = base85)
                fixed (byte* pResult = result)
                {
                    uint* pUint = (uint*)pResult;
                    int base85Pos = 0;
                    int outputPos = 0;
                    var zeroChar = options.Zero;
                    var alphabetMap = BuildAlphabetMap(alphabet);

                    for (int i = 0; i < blockCount; i++)
                    {
                        if (zeroChar != '\0' && pBase85[base85Pos] == zeroChar)
                        {
                            *pUint++ = 0;
                            base85Pos++;
                            outputPos += 4;
                            continue;
                        }

                        *pUint++ = DecodeBlock(pBase85 + base85Pos, 5, alphabetMap, variant);
                        base85Pos += 5;
                        outputPos += 4;
                    }

                    if (remainingChars > 0)
                    {
                        uint lastBlock = DecodeBlock(pBase85 + base85Pos, remainingChars, alphabetMap, variant);
                        // 从大端序高位开始提取字节
                        for (int i = 0; i < remainingChars - 1; i++)
                        {
                            result[outputPos++] = (byte)(lastBlock >> (24 - i * 8));
                        }
                    }
                }
            }

            return result;
        }

        public static string DecodeToString(string base85, StringEncoding encoding = StringEncoding.UTF8, Base85Variant variant = Base85Variant.Ascii85)
        {
            byte[] data = Decode(base85, variant);
            return GetString(data, encoding);
        }
        #endregion

        #region 私有辅助方法
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int EncodeBlock(uint value, char* output, Base85Variant variant)
        {
            var alphabet = VariantAlphabets[variant];
            char* current = output + 4;  // 从末尾开始写入

            // 手动实现除法
            for (int i = 0; i < 5; i++)
            {
                uint remainder = value % 85;
                value /= 85;
                *current-- = alphabet[remainder];
            }

            return 5;
        }

        private static unsafe uint DecodeBlock(char* input, int length, byte[] alphabetMap, Base85Variant variant)
        {
            uint result = 0;
            for (int i = 0; i < length; i++)
            {
                char c = input[i];
                if (c < 0 || c >= alphabetMap.Length || alphabetMap[c] == 0xFF)
                    throw new FormatException($"Invalid character '{c}' in Base85 string");

                result = result * 85 + alphabetMap[c];
            }

            // 填充剩余部分
            for (int i = length; i < 5; i++)
            {
                result = result * 85 + 84;
            }

            // 转换为系统字节序
            return BitConverter.IsLittleEndian ? ReverseBytes(result) : result;
        }

        private static byte[] BuildAlphabetMap(char[] alphabet)
        {
            var map = new byte[256];
            Array.Fill(map, (byte)0xFF);

            for (byte i = 0; i < alphabet.Length; i++)
            {
                char c = alphabet[i];
                map[c] = i;
            }
            return map;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReverseBytes(uint value)
        {
            return (value >> 24) |
                   ((value >> 8) & 0x0000FF00) |
                   ((value << 8) & 0x00FF0000) |
                   (value << 24);
        }

        private static byte[] GetBytes(string text, StringEncoding encoding)
        {
            return encoding switch
            {
                StringEncoding.ASCII => Encoding.ASCII.GetBytes(text),
                StringEncoding.UTF16LE => Encoding.Unicode.GetBytes(text),
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetBytes(text),
                StringEncoding.UTF32 => Encoding.UTF32.GetBytes(text),
#pragma warning disable SYSLIB0001, CS0618
                StringEncoding.UTF7 => Encoding.UTF7.GetBytes(text),
#pragma warning restore SYSLIB0001, CS0618
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1.GetBytes(text),
#endif
                _ => Encoding.UTF8.GetBytes(text) // UTF8 是默认值
            };
        }

        private static string GetString(byte[] data, StringEncoding encoding)
        {
            return encoding switch
            {
                StringEncoding.ASCII => Encoding.ASCII.GetString(data),
                StringEncoding.UTF16LE => Encoding.Unicode.GetString(data),
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetString(data),
                StringEncoding.UTF32 => Encoding.UTF32.GetString(data),
#pragma warning disable SYSLIB0001, CS0618
                StringEncoding.UTF7 => Encoding.UTF7.GetString(data),
#pragma warning restore SYSLIB0001, CS0618
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1.GetString(data),
#endif
                _ => Encoding.UTF8.GetString(data) // UTF8 是默认值
            };
        }
        #endregion

        #region 特殊变体方法
        public static string EncodeZ85(byte[] data) => Encode(data, Base85Variant.Z85);
        public static byte[] DecodeZ85(string base85) => Decode(base85, Base85Variant.Z85);
        public static string EncodeGit(byte[] data) => Encode(data, Base85Variant.Git);
        public static byte[] DecodeGit(string base85) => Decode(base85, Base85Variant.Git);
        #endregion
    }
}
#endif
