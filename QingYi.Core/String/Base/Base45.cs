using System;
using System.Text;

#pragma warning disable CA1510, CS0618, SYSLIB0001, IDE0300, IDE0301

namespace QingYi.Core.String.Base
{
    /// <summary>
    /// Base45 codec library.<br />
    /// Base45 编解码库。
    /// </summary>
    public unsafe class Base45
    {
        private const string EncodingTable = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";
        private static readonly byte[] DecodingTable = new byte[256];

        static Base45()
        {
            for (int i = 0; i < DecodingTable.Length; i++)
                DecodingTable[i] = 0xFF;

            for (byte i = 0; i < EncodingTable.Length; i++)
            {
                char c = EncodingTable[i];
                DecodingTable[c] = i;
            }
        }

        /// <summary>
        /// Gets the base45-encoded character set.<br />
        /// 获取 Base45 编码的字符集。
        /// </summary>
        /// <returns>The base45-encoded character set.<br />Base45 编码的字符集</returns>
        public override string ToString() => EncodingTable;

        /// <summary>
        /// Base45 encoding of the string.<br />
        /// 将字符串进行Base45编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string Encode(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            byte[] bytes = GetEncoding(encoding).GetBytes(input);
            return EncodeBytes(bytes);
        }

        /// <summary>
        /// Base45 decoding of the string.<br />
        /// 将字符串进行Base45解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string Decode(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            byte[] bytes = DecodeString(input);
            return GetEncoding(encoding).GetString(bytes);
        }

        private static string EncodeBytes(byte[] b)
        {
            int inputLength = b.Length;
            int outputLength = (inputLength / 2) * 3 + (inputLength % 2 == 1 ? 2 : 0);
            char[] output = new char[outputLength];

            fixed (byte* inputPtr = b)
            fixed (char* outputPtr = output)
            {
                byte* ip = inputPtr;
                char* op = outputPtr;

                for (int i = 0; i < inputLength - 1; i += 2)
                {
                    int value = (*ip++) << 8 | *ip++;
                    int d3 = value % 45;
                    value /= 45;
                    int d2 = value % 45;
                    value /= 45;
                    int d1 = value;

                    *op++ = EncodingTable[d1];
                    *op++ = EncodingTable[d2];
                    *op++ = EncodingTable[d3];
                }

                if (inputLength % 2 == 1)
                {
                    int value = *ip;
                    int d2 = value % 45;
                    value /= 45;
                    int d1 = value;

                    *op++ = EncodingTable[d1];
                    *op++ = EncodingTable[d2];
                }
            }
            return new string(output);
        }

        private static byte[] DecodeString(string input)
        {
            int inputLength = input.Length;
            if (inputLength == 0) return Array.Empty<byte>();

            int remainder = inputLength % 3;
            if (remainder == 1)
                throw new ArgumentException("Invalid Base45 string length.");

            int outputLength = (inputLength / 3) * 2 + (remainder == 2 ? 1 : 0);
            byte[] output = new byte[outputLength];

            fixed (char* inputPtr = input)
            fixed (byte* outputPtr = output)
            {
                char* inPtr = inputPtr;
                byte* outPtr = outputPtr;

                for (int i = 0; i < inputLength - remainder; i += 3)
                {
                    int d1 = GetValue(*inPtr++);
                    int d2 = GetValue(*inPtr++);
                    int d3 = GetValue(*inPtr++);

                    int value = d1 * 45 * 45 + d2 * 45 + d3;
                    if (value > 0xFFFF) throw new FormatException("Invalid Base45 triplet.");

                    *outPtr++ = (byte)(value >> 8);
                    *outPtr++ = (byte)value;
                }

                if (remainder == 2)
                {
                    int d1 = GetValue(*inPtr++);
                    int d2 = GetValue(*inPtr++);

                    int value = d1 * 45 + d2;
                    if (value > 0xFF) throw new FormatException("Invalid Base45 pair.");
                    *outPtr++ = (byte)value;
                }
            }

            return output;
        }

        private static int GetValue(char c)
        {
            if (c > 255)
                throw new FormatException($"Invalid character '{(int)c}'");

            byte v = DecodingTable[c]; // value

            if (v == 0xFF)
                throw new FormatException($"Invalid character '{c}'");

            return v;
        }

        private static Encoding GetEncoding(StringEncoding encoding)
        {
            return encoding switch
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
                _ => throw new NotSupportedException("Unsupported encoding"),
            };
        }
    }

    /// <summary>
    /// Static string extension of Base45 codec library.<br />
    /// Base45 编解码库的静态字符串拓展。
    /// </summary>
    public static class Base45Extension
    {
        /// <summary>
        /// Base45 encoding of the string.<br />
        /// 将字符串进行 Base45 编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string EncodeBase45(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base45.Encode(input, encoding);

        /// <summary>
        /// Base45 decoding of the string.<br />
        /// 将字符串进行 Base45 解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string DecodeBase45(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base45.Decode(input, encoding);
    }
}
