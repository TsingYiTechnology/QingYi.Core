#if NET5_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace QingYi.Core.Codec.Base
{
    /// <summary>
    /// Base122编解码器
    /// </summary>
    public sealed class Base122
    {
        /// <summary>
        /// Base122变体
        /// </summary>
        public enum Variant
        {
            /// <summary>
            /// 标准Base122编码
            /// </summary>
            Standard
        }

        private const int BLOCK_BITS = 56;
        private const int BLOCK_BYTES = 7;
        private const int BLOCK_CHARS = 8;

        private static readonly char[] EncodingTable;
        private static readonly Dictionary<char, byte> DecodingTable;

        static Base122()
        {
            EncodingTable = new char[122];
            DecodingTable = new Dictionary<char, byte>(122);

            int index = 0;
            // 添加U+0080到U+00FF范围的字符（跳过控制字符和特殊字符）
            for (int i = 0x80; i <= 0xFF; i++)
            {
                char c = (char)i;

                // 跳过控制字符和问题字符
                if (IsValidBase122Char(c))
                {
                    EncodingTable[index] = c;
                    DecodingTable[c] = (byte)index;
                    index++;
                }
            }

            // 添加补充字符
            char[] supplementaryChars = {
            '\u2500', '\u2501', '\u2502', '\u2503', '\u2504', '\u2505',
            '\u2506', '\u2507', '\u2508', '\u2509', '\u250A', '\u250B'
        };

            foreach (char c in supplementaryChars)
            {
                if (index >= 122) break;

                EncodingTable[index] = c;
                DecodingTable[c] = (byte)index;
                index++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidBase122Char(char c)
        {
            // 跳过控制字符、双引号和反斜杠
            return !char.IsControl(c) && c != '"' && c != '\\';
        }

        /// <summary>
        /// 获取Base122字符集
        /// </summary>
        public override string ToString() => new string(EncodingTable);

        /// <summary>
        /// 编码字节数组为Base122字符串
        /// </summary>
        public static string Encode(byte[] input) => Encode(input, Variant.Standard);

        /// <summary>
        /// 编码字节数组为Base122字符串（指定变体）
        /// </summary>
        public static unsafe string Encode(byte[] input, Variant variant)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length == 0) return string.Empty;

            int outputLength = (input.Length + BLOCK_BYTES - 1) / BLOCK_BYTES * BLOCK_CHARS;
            char[] outputBuffer = new char[outputLength];

            fixed (byte* inputPtr = input)
            fixed (char* outputPtr = outputBuffer)
            fixed (char* tablePtr = EncodingTable)
            {
                byte* inPtr = inputPtr;
                char* outPtr = outputPtr;

                int remaining = input.Length;
                while (remaining >= BLOCK_BYTES)
                {
                    EncodeBlock(inPtr, outPtr, tablePtr);
                    inPtr += BLOCK_BYTES;
                    outPtr += BLOCK_CHARS;
                    remaining -= BLOCK_BYTES;
                }

                if (remaining > 0)
                {
                    byte* tempBlock = stackalloc byte[BLOCK_BYTES];
                    Buffer.MemoryCopy(inPtr, tempBlock, BLOCK_BYTES, remaining);
                    EncodeBlock(tempBlock, outPtr, tablePtr);
                }
            }

            return new string(outputBuffer);
        }

        private static unsafe void EncodeBlock(byte* input, char* output, char* table)
        {
            ulong block = 0;

            // 将7字节加载为56位整数（大端序）
            for (int i = 0; i < BLOCK_BYTES; i++)
            {
                block = (block << 8) | input[i];
            }

            // 提取8个7位组
            for (int i = 0; i < BLOCK_CHARS; i++)
            {
                int shift = BLOCK_BITS - (i + 1) * 7;
                byte value = (byte)((block >> shift) & 0x7F);
                output[i] = table[value];
            }
        }

        /// <summary>
        /// 编码字符串为Base122字符串
        /// </summary>
        public static string Encode(string input, StringEncoding encoding) =>
            Encode(GetBytes(input, encoding));

        /// <summary>
        /// 解码Base122字符串为字节数组
        /// </summary>
        public static byte[] Decode(string input) => Decode(input, Variant.Standard);

        /// <summary>
        /// 解码Base122字符串为字节数组（指定变体）
        /// </summary>
        public static unsafe byte[] Decode(string input, Variant variant)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length == 0) return Array.Empty<byte>();
            if (input.Length % BLOCK_CHARS != 0)
                throw new ArgumentException("Invalid Base122 input length");

            int outputLength = input.Length / BLOCK_CHARS * BLOCK_BYTES;
            byte[] outputBuffer = new byte[outputLength];

            fixed (char* inputPtr = input)
            fixed (byte* outputPtr = outputBuffer)
            {
                char* inPtr = inputPtr;
                byte* outPtr = outputPtr;

                int blocks = input.Length / BLOCK_CHARS;
                for (int i = 0; i < blocks; i++)
                {
                    DecodeBlock(inPtr, outPtr);
                    inPtr += BLOCK_CHARS;
                    outPtr += BLOCK_BYTES;
                }
            }

            return outputBuffer;
        }

        private static unsafe void DecodeBlock(char* input, byte* output)
        {
            ulong block = 0;

            for (int i = 0; i < BLOCK_CHARS; i++)
            {
                char c = input[i];
                if (!DecodingTable.TryGetValue(c, out byte value))
                    throw new FormatException($"Invalid Base122 character: {c}");

                block = (block << 7) | value;
            }

            // 提取7个字节
            for (int i = 0; i < BLOCK_BYTES; i++)
            {
                int shift = BLOCK_BITS - (i + 1) * 8;
                output[i] = (byte)((block >> shift) & 0xFF);
            }
        }

        /// <summary>
        /// 解码Base122字符串为原始字符串
        /// </summary>
        public static string DecodeToString(string input, StringEncoding encoding) =>
            GetString(Decode(input), encoding);

        private static byte[] GetBytes(string input, StringEncoding encoding)
        {
            return encoding switch
            {
                StringEncoding.UTF8 => Encoding.UTF8.GetBytes(input),
                StringEncoding.UTF16LE => Encoding.Unicode.GetBytes(input),
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetBytes(input),
                StringEncoding.ASCII => Encoding.ASCII.GetBytes(input),
                StringEncoding.UTF32 => Encoding.UTF32.GetBytes(input),
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1.GetBytes(input),
#endif
                StringEncoding.UTF7 => Encoding.UTF7.GetBytes(input),
                _ => throw new NotSupportedException("Unsupported encoding")
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
                StringEncoding.UTF7 => Encoding.UTF7.GetString(bytes),
                _ => throw new NotSupportedException("Unsupported encoding")
            };
        }
    }
}
#endif
