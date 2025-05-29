#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
using System;
using System.Text;

#pragma warning disable SYSLIB0001, CS0618

namespace QingYi.Core.Codec.Base
{
    /// <summary>
    /// Base32 codec library (Geohash).<br />
    /// Base32 编解码库（Geohash）。
    /// </summary>
    public class Base32GeoHash
    {
        private const string Base32Chars = "0123456789bcdefghjkmnpqrstuvwxyz";
        private static readonly char[] EncodeTable = Base32Chars.ToCharArray();
        private static readonly byte[] DecodeTable = new byte[128];

        static Base32GeoHash()
        {
            Array.Fill(DecodeTable, (byte)0xFF);
            for (byte i = 0; i < EncodeTable.Length; i++)
            {
                DecodeTable[EncodeTable[i]] = i;
            }
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
        public static string Encode(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            byte[] bytes = GetBytes(input, encoding);
            int byteCount = bytes.Length;
            int bitCount = byteCount * 8;
            int charCount = (bitCount + 4) / 5; // Ceiling(bitCount / 5)
            char[] result = new char[charCount];

            unsafe
            {
                fixed (byte* bytesPtr = bytes)
                fixed (char* resultPtr = result)
                {
                    byte* currentByte = bytesPtr;
                    byte* endByte = bytesPtr + byteCount;
                    char* currentChar = resultPtr;

                    int buffer = 0;
                    int bufferBits = 0;

                    while (currentByte < endByte)
                    {
                        buffer = (buffer << 8) | *currentByte++;
                        bufferBits += 8;

                        while (bufferBits >= 5)
                        {
                            bufferBits -= 5;
                            int index = (buffer >> bufferBits) & 0x1F;
                            *currentChar++ = EncodeTable[index];
                        }
                    }

                    if (bufferBits > 0)
                    {
                        int index = (buffer << (5 - bufferBits)) & 0x1F;
                        *currentChar++ = EncodeTable[index];
                    }
                }
            }

            return new string(result);
        }

        /// <summary>
        /// Base36 decoding of the string.<br />
        /// 将字符串进行Base32解码。
        /// </summary>
        /// <param name="base32Input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string Decode(string base32Input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (base32Input == null)
                throw new ArgumentNullException(nameof(base32Input));

            int charCount = base32Input.Length;
            int bitCount = charCount * 5;
            int byteCount = bitCount / 8;
            byte[] bytes = new byte[byteCount];

            unsafe
            {
                fixed (char* inputPtr = base32Input)
                fixed (byte* bytesPtr = bytes)
                {
                    char* currentChar = inputPtr;
                    char* endChar = inputPtr + charCount;
                    byte* currentByte = bytesPtr;
                    byte* endByte = bytesPtr + byteCount;

                    int buffer = 0;
                    int bufferBits = 0;

                    while (currentChar < endChar)
                    {
                        char c = *currentChar++;
                        if (c >= DecodeTable.Length || DecodeTable[c] == 0xFF)
                            throw new FormatException($"Invalid Base32 character: '{c}'");

                        int value = DecodeTable[c];
                        buffer = (buffer << 5) | value;
                        bufferBits += 5;

                        while (bufferBits >= 8 && currentByte < endByte)
                        {
                            bufferBits -= 8;
                            *currentByte++ = (byte)((buffer >> bufferBits) & 0xFF);
                        }
                    }
                }
            }

            return GetString(bytes, encoding);
        }

        private static byte[] GetBytes(string input, StringEncoding encoding)
        {
            Encoding encoder = encoding switch
            {
                StringEncoding.UTF8 => Encoding.UTF8,
                StringEncoding.UTF16LE => Encoding.Unicode,
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode,
                StringEncoding.UTF32 => Encoding.UTF32,
                StringEncoding.UTF7 => Encoding.UTF7,
                StringEncoding.ASCII => Encoding.ASCII,
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1,
#endif
                _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
            };
            return encoder.GetBytes(input);
        }

        private static string GetString(byte[] bytes, StringEncoding encoding)
        {
            Encoding decoder = encoding switch
            {
                StringEncoding.UTF8 => Encoding.UTF8,
                StringEncoding.UTF16LE => Encoding.Unicode,
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode,
                StringEncoding.UTF32 => Encoding.UTF32,
                StringEncoding.UTF7 => Encoding.UTF7,
                StringEncoding.ASCII => Encoding.ASCII,
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1,
#endif
                _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
            };
            return decoder.GetString(bytes);
        }
    }
}
#endif