using System.Text;
using System;

#pragma warning disable SYSLIB0001, CS0618

namespace QingYi.Core.String.Base
{
    /// <summary>
    /// Base32 codec library (z-base-32).<br />
    /// Base32 编解码库（z-base-32）。
    /// </summary>
    public class Base32z
    {
        private const string ZBase32Chars = "ybndrfg8ejkmcpqxot1uwisza345h769";
        private static readonly byte[] ReverseTable = new byte[128];

        static Base32z()
        {
            for (int i = 0; i < ReverseTable.Length; i++)
                ReverseTable[i] = 0xFF;

            for (byte i = 0; i < ZBase32Chars.Length; i++)
            {
                char c = ZBase32Chars[i];
                if (c >= ReverseTable.Length)
                    throw new InvalidOperationException("Invalid character in Z-Base-32 charset.");
                ReverseTable[c] = i;
            }
        }

        /// <summary>
        /// Gets the base32-encoded character set.<br />
        /// 获取 Base32 编码的字符集。
        /// </summary>
        /// <returns>The base32-encoded character set.<br />Base32 编码的字符集</returns>
        public override string ToString() => ZBase32Chars;

        /// <summary>
        /// Base36 encoding of the string.<br />
        /// 将字符串进行Base32编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string Encode(string input, StringEncoding encoding)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            byte[] bytes = GetBytes(input, encoding);
            return EncodeToString(bytes);
        }

        /// <summary>
        /// Base36 decoding of the string.<br />
        /// 将字符串进行Base32解码。
        /// </summary>
        /// <param name="base32">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string Decode(string base32, StringEncoding encoding)
        {
            if (base32 == null)
                throw new ArgumentNullException(nameof(base32));

            byte[] bytes = DecodeToBytes(base32);
            return GetString(bytes, encoding);
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

        private static string EncodeToString(byte[] bytes)
        {
            if (bytes.Length == 0)
                return string.Empty;

            int byteCount = bytes.Length;
            int outputLength = (byteCount * 8 + 4) / 5; // ceil(bitCount/5)
            char[] output = new char[outputLength];

            ulong buffer = 0;
            int bitsInBuffer = 0;
            int outputPos = 0;

            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    byte* current = ptr;
                    byte* end = ptr + byteCount;
                    while (current < end)
                    {
                        buffer = (buffer << 8) | *current++;
                        bitsInBuffer += 8;

                        while (bitsInBuffer >= 5)
                        {
                            int index = (int)((buffer >> (bitsInBuffer - 5)) & 0x1F);
                            output[outputPos++] = ZBase32Chars[index];
                            bitsInBuffer -= 5;
                            buffer &= (1UL << bitsInBuffer) - 1;
                        }
                    }
                }
            }

            if (bitsInBuffer > 0)
            {
                buffer <<= (5 - bitsInBuffer);
                int index = (int)(buffer & 0x1F);
                output[outputPos++] = ZBase32Chars[index];
            }

            return new string(output);
        }

        private static byte[] DecodeToBytes(string base32)
        {
            if (base32.Length == 0)
                return Array.Empty<byte>();

            int inputLength = base32.Length;
            int bitCount = inputLength * 5;
            int byteCount = (bitCount + 7) / 8;
            byte[] output = new byte[byteCount];

            ulong buffer = 0;
            int bitsInBuffer = 0;
            int outputPos = 0;

            unsafe
            {
                fixed (char* inputPtr = base32)
                {
                    char* current = inputPtr;
                    char* end = inputPtr + inputLength;
                    while (current < end)
                    {
                        char c = *current++;
                        if (c >= ReverseTable.Length || ReverseTable[c] == 0xFF)
                            throw new ArgumentException($"Invalid character '{c}' in Base32 string.");

                        byte value = ReverseTable[c];
                        buffer = (buffer << 5) | value;
                        bitsInBuffer += 5;

                        while (bitsInBuffer >= 8)
                        {
                            byte b = (byte)(buffer >> (bitsInBuffer - 8));
                            output[outputPos++] = b;
                            bitsInBuffer -= 8;
                            buffer &= (1UL << bitsInBuffer) - 1;
                        }
                    }
                }
            }

            if (bitsInBuffer > 0)
            {
                buffer <<= (8 - bitsInBuffer);
                output[outputPos++] = (byte)buffer;
            }

            if (outputPos < output.Length)
                Array.Resize(ref output, outputPos);

            return output;
        }
    }
}
