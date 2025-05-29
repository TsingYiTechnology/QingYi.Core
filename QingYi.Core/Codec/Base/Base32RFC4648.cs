using System;
using System.Text;

#pragma warning disable CA1510, CS0618, SYSLIB0001, IDE0300, IDE0301

namespace QingYi.Core.Codec.Base
{
    /// <summary>
    /// Base32 codec library (RFC4648).<br />
    /// Base32 编解码库（RFC4648）。
    /// </summary>
    public class Base32RFC4648
    {
        private const string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        private static readonly byte[] DecodeTable = new byte[128];

        static Base32RFC4648()
        {
            for (int i = 0; i < DecodeTable.Length; i++)
                DecodeTable[i] = 0xFF;
            for (int i = 0; i < Base32Chars.Length; i++)
                DecodeTable[Base32Chars[i]] = (byte)i;
        }

        /// <summary>
        /// Gets the base32-encoded character set.<br />
        /// 获取 Base32 编码的字符集。
        /// </summary>
        /// <returns>The base32-encoded character set.<br />Base32 编码的字符集</returns>
        public override string ToString() => Base32Chars;

        /// <summary>
        /// Base36 encoding of the string.<br />
        /// 将字符串进行Base32编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string Encode(string input, StringEncoding encoding)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            byte[] bytes = GetBytes(input, encoding);
            return EncodeInternal(bytes);
        }

        /// <summary>
        /// Base36 decoding of the string.<br />
        /// 将字符串进行Base32解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string Decode(string input, StringEncoding encoding)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length % 8 != 0)
                throw new FormatException("Input length must be a multiple of 8.");

            int paddingCount = CalculatePadding(input);
            int r = GetRemainingBytes(paddingCount);
            int validCharCount = input.Length - paddingCount;
            int validBits = validCharCount * 5 - GetTruncateBits(r);
            if (validBits % 8 != 0)
                throw new FormatException("Invalid data length.");

            byte[] bytes = DecodeInternal(input, validBits / 8, paddingCount);
            return GetString(bytes, encoding);
        }

        private static unsafe string EncodeInternal(byte[] bytes)
        {
            int outputLen = (bytes.Length + 4) / 5 * 8;
            char[] output = new char[outputLen];
            fixed (byte* inputPtr = bytes)
            fixed (char* outputPtr = output)
            {
                byte* pInput = inputPtr;
                char* pOutput = outputPtr;
                int remaining = bytes.Length;

                while (remaining >= 5)
                {
                    EncodeBlock(pInput, pOutput);
                    pInput += 5;
                    pOutput += 8;
                    remaining -= 5;
                }

                if (remaining > 0)
                    EncodeRemaining(pInput, remaining, pOutput, outputLen - (int)(pOutput - outputPtr));
            }
            return new string(output);
        }

        private static unsafe void EncodeBlock(byte* input, char* output)
        {
            ulong buffer = (ulong)input[0] << 32 | (ulong)input[1] << 24 |
                           (ulong)input[2] << 16 | (ulong)input[3] << 8 | input[4];
            for (int i = 0; i < 8; i++)
                output[i] = Base32Chars[(int)(buffer >> 35 - i * 5 & 0x1F)];
        }

        private static unsafe void EncodeRemaining(byte* input, int remaining, char* output, int outputRemaining)
        {
            ulong buffer = 0;
            for (int i = 0; i < remaining; i++)
                buffer |= (ulong)input[i] << 8 * (4 - i);

            int bits = remaining * 8;
            int charCount = (bits + 4) / 5;
            charCount = Math.Min(charCount, outputRemaining);

            for (int i = 0; i < charCount; i++)
                output[i] = Base32Chars[(int)(buffer >> 35 - i * 5 & 0x1F)];

            for (int i = charCount; i < 8; i++)
                output[i] = '=';
        }

        private static unsafe byte[] DecodeInternal(string input, int byteCount, int paddingCount)
        {
            byte[] bytes = new byte[byteCount];
            fixed (char* inputPtr = input)
            fixed (byte* outputPtr = bytes)
            {
                char* pInput = inputPtr;
                byte* pOutput = outputPtr;
                int blocks = (input.Length - paddingCount) / 8;

                for (int i = 0; i < blocks; i++)
                {
                    DecodeBlock(pInput, pOutput);
                    pInput += 8;
                    pOutput += 5;
                }

                if (paddingCount > 0)
                    DecodeLastBlock(pInput, pOutput, paddingCount);
            }
            return bytes;
        }

        private static unsafe void DecodeBlock(char* input, byte* output)
        {
            ulong buffer = 0;
            for (int i = 0; i < 8; i++)
            {
                char c = input[i];
                if (c >= 128 || DecodeTable[c] == 0xFF)
                    throw new FormatException($"Invalid character '{c}'.");

                buffer = buffer << 5 | DecodeTable[c];
            }

            output[0] = (byte)(buffer >> 32);
            output[1] = (byte)(buffer >> 24);
            output[2] = (byte)(buffer >> 16);
            output[3] = (byte)(buffer >> 8);
            output[4] = (byte)buffer;
        }

        private static unsafe void DecodeLastBlock(char* input, byte* output, int paddingCount)
        {
            ulong buffer = 0;
            int validChars = 8 - paddingCount;
            for (int i = 0; i < validChars; i++)
            {
                char c = input[i];
                if (c >= 128 || DecodeTable[c] == 0xFF)
                    throw new FormatException($"Invalid character '{c}'.");

                buffer = buffer << 5 | DecodeTable[c];
            }

            buffer <<= paddingCount * 5;
            int bytesToWrite = validChars * 5 / 8;
            for (int i = 0; i < bytesToWrite; i++)
                output[i] = (byte)(buffer >> 32 - i * 8);
        }

        private static byte[] GetBytes(string input, StringEncoding encoding)
        {
            Encoding encoder;
            switch (encoding)
            {
                case StringEncoding.UTF8:
                    encoder = Encoding.UTF8;
                    break;
                case StringEncoding.UTF16LE:
                    encoder = Encoding.Unicode;
                    break;
                case StringEncoding.UTF16BE:
                    encoder = Encoding.BigEndianUnicode;
                    break;
                case StringEncoding.UTF32:
                    encoder = Encoding.UTF32;
                    break;
                case StringEncoding.UTF7:
                    encoder = Encoding.UTF7;
                    break;
#if NET6_0_OR_GREATER
                case StringEncoding.Latin1:
                    encoder = Encoding.Latin1;
                    break;
#endif
                case StringEncoding.ASCII:
                    encoder = Encoding.ASCII;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(encoding));
            }
            return encoder.GetBytes(input);
        }

        private static string GetString(byte[] bytes, StringEncoding encoding)
        {
            Encoding decoder;
            switch (encoding)
            {
                case StringEncoding.UTF8:
                    decoder = Encoding.UTF8;
                    break;
                case StringEncoding.UTF16LE:
                    decoder = Encoding.Unicode;
                    break;
                case StringEncoding.UTF16BE:
                    decoder = Encoding.BigEndianUnicode;
                    break;
                case StringEncoding.ASCII:
                    decoder = Encoding.ASCII;
                    break;
                case StringEncoding.UTF32:
                    decoder = Encoding.UTF32;
                    break;
#if NET6_0_OR_GREATER
                case StringEncoding.Latin1:
                    decoder = Encoding.Latin1;
                    break;
#endif
                case StringEncoding.UTF7:
                    decoder = Encoding.UTF7;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(encoding));
            }
            return decoder.GetString(bytes);
        }

        private static int CalculatePadding(string input)
        {
            int paddingCount = 0;
            for (int i = input.Length - 1; i >= 0 && input[i] == '='; i--)
                paddingCount++;
            return paddingCount;
        }

        private static int GetRemainingBytes(int paddingCount)
        {
            switch (paddingCount)
            {
                case 0:
                    return 0;
                case 1:
                    return 4;
                case 3:
                    return 3;
                case 4:
                    return 2;
                case 6:
                    return 1;
                default:
                    throw new FormatException("Invalid padding.");
            }
        }

        private static int GetTruncateBits(int r)
        {
            switch (r)
            {
                case 0:
                    return 0;
                case 1:
                    return 2;
                case 2:
                    return 4;
                case 3:
                    return 1;
                case 4:
                    return 3;
                default:
                    throw new ArgumentOutOfRangeException(nameof(r));
            }
        }
    }
}
