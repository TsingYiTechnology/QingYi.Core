#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
using System;
using System.Text;

#pragma warning disable SYSLIB0001, CS0618

namespace QingYi.Core.Codec.Base
{
    /// <summary>
    /// Base32 codec library (Base 32 Encoding with Extended Hex Alphabet per §7).<br />
    /// Base32 编解码库（基于十六进制扩展的 Base 32 编码字母表 §7）。
    /// </summary>
    public class Base32ExtendedHex
    {
        private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUV";
        private const char PaddingChar = '=';
        private static readonly byte[] DecodeMap = new byte[256];

        static Base32ExtendedHex()
        {
            Array.Fill(DecodeMap, (byte)0xFF);
            for (byte i = 0; i < Alphabet.Length; i++)
                DecodeMap[Alphabet[i]] = i;
            DecodeMap[PaddingChar] = 0xFE;
        }

        /// <summary>
        /// Gets the base32-encoded character set.<br />
        /// 获取 Base32 编码的字符集。
        /// </summary>
        /// <returns>The base32-encoded character set.<br />Base32 编码的字符集</returns>
        public override string ToString() => Alphabet;

        /// <summary>
        /// Base36 encoding of the string.<br />
        /// 将字符串进行Base32编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string Encode(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            byte[] bytes = GetBytes(input, encoding);
            return ConvertToBase32(bytes);
        }

        /// <summary>
        /// Base36 decoding of the string.<br />
        /// 将字符串进行Base32解码。
        /// </summary>
        /// <param name="base32">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string Decode(string base32, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (base32 == null) throw new ArgumentNullException(nameof(base32));
            byte[] bytes = ConvertFromBase32(base32);
            return GetString(bytes, encoding);
        }

        private static unsafe string ConvertToBase32(byte[] input)
        {
            if (input.Length == 0) return string.Empty;

            int base32Length = (input.Length * 8 + 4) / 5;
            int paddingLength = (8 - base32Length % 8) % 8;
            char[] output = new char[base32Length + paddingLength];

            fixed (byte* inputPtr = input)
            fixed (char* outputPtr = output)
            {
                byte* currentInput = inputPtr;
                byte* endInput = inputPtr + input.Length;
                char* currentOutput = outputPtr;

                ulong buffer = 0;
                int bitsInBuffer = 0;

                while (currentInput < endInput)
                {
                    buffer = (buffer << 8) | *currentInput++;
                    bitsInBuffer += 8;

                    while (bitsInBuffer >= 5)
                    {
                        bitsInBuffer -= 5;
                        byte value = (byte)((buffer >> bitsInBuffer) & 0x1F);
                        *currentOutput++ = Alphabet[value];
                    }
                }

                if (bitsInBuffer > 0)
                {
                    buffer <<= (5 - bitsInBuffer);
                    byte value = (byte)(buffer & 0x1F);
                    *currentOutput++ = Alphabet[value];
                }

                while (currentOutput < outputPtr + output.Length)
                {
                    *currentOutput++ = PaddingChar;
                }
            }

            return new string(output);
        }

        private static unsafe byte[] ConvertFromBase32(string base32)
        {
            if (base32.Length == 0) return Array.Empty<byte>();

            // Calculate valid length ignoring padding
            int validLength = base32.Length;
            while (validLength > 0 && base32[validLength - 1] == PaddingChar)
                validLength--;

            // Calculate expected bytes and padding bits
            int totalBits = validLength * 5;
            int paddingBits = totalBits % 8 == 0 ? 0 : 8 - (totalBits % 8);
            int expectedBytes = (totalBits - paddingBits) / 8;

            byte[] output = new byte[expectedBytes];

            fixed (char* inputPtr = base32)
            fixed (byte* outputPtr = output)
            {
                char* currentInput = inputPtr;
                char* endInput = inputPtr + validLength;
                byte* currentOutput = outputPtr;
                byte* endOutput = outputPtr + expectedBytes;

                ulong buffer = 0;
                int bitsInBuffer = 0;

                while (currentInput < endInput)
                {
                    char c = *currentInput++;
                    byte value = DecodeMap[c];

                    if (value >= 0x20)
                    {
                        if (value == 0xFE)
                            throw new ArgumentException($"Invalid padding position: {c}");
                        throw new ArgumentException($"Invalid Base32 character: {c}");
                    }

                    buffer = (buffer << 5) | value;
                    bitsInBuffer += 5;

                    // Write bytes while we have enough bits
                    while (bitsInBuffer >= 8 && currentOutput < endOutput)
                    {
                        bitsInBuffer -= 8;
                        *currentOutput++ = (byte)((buffer >> bitsInBuffer) & 0xFF);
                    }
                }

                // Verify remaining bits match padding requirements
                if (bitsInBuffer != paddingBits)
                    throw new ArgumentException("Invalid padding");

                if (paddingBits > 0)
                {
                    ulong mask = (1UL << paddingBits) - 1;
                    if ((buffer & mask) != 0)
                        throw new ArgumentException("Invalid padding");
                }

                if (currentOutput != endOutput)
                    throw new ArgumentException("Invalid padding");
            }

            return output;
        }

        private static byte[] GetBytes(string input, StringEncoding encoding) => GetEncoding(encoding).GetBytes(input);

        private static string GetString(byte[] bytes, StringEncoding encoding) => GetEncoding(encoding).GetString(bytes);

        private static Encoding GetEncoding(StringEncoding encoding)
        {
            return encoding switch
            {
                StringEncoding.UTF8 => Encoding.UTF8,
                StringEncoding.UTF16LE => Encoding.Unicode,
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode,
                StringEncoding.UTF32 => Encoding.UTF32,
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1,
#endif
                StringEncoding.UTF7 => Encoding.UTF7,
                StringEncoding.ASCII => Encoding.ASCII,
                _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
            };
        }
    }
}
#endif