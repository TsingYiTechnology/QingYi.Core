#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
using System;
using System.Text;

namespace QingYi.Core.String.Base
{
    /// <summary>
    /// Base56 codec library.<br />
    /// Base56 编解码库。
    /// </summary>
    public unsafe class Base56
    {
        private const string Base56Chars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        private static readonly int[] CharToIndexMap = new int[128];

        static Base56()
        {
            Array.Fill(CharToIndexMap, -1);
            for (int i = 0; i < Base56Chars.Length; i++)
            {
                CharToIndexMap[Base56Chars[i]] = i;
            }
        }

        /// <summary>
        /// Gets the base56-encoded character set.<br />
        /// 获取 Base56 编码的字符集。
        /// </summary>
        /// <returns>The base56-encoded character set.<br />Base56 编码的字符集</returns>
        public override string ToString() => Base56Chars;

        /// <summary>
        /// Base56 encoding of the string.<br />
        /// 将字符串进行 Base56 编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string EncodeString(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            var bytes = GetEncoding(encoding).GetBytes(input);
            return Encode(bytes);
        }

        /// <summary>
        /// Base56 decoding of the string.<br />
        /// 将字符串进行 Base56 解码。
        /// </summary>
        /// <param name="base56String">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string DecodeString(string base56String, StringEncoding encoding = StringEncoding.UTF8)
        {
            var bytes = Decode(base56String);
            return GetEncoding(encoding).GetString(bytes);
        }

        private static Encoding GetEncoding(StringEncoding encoding)
        {
            return encoding switch
            {
                StringEncoding.UTF8 => Encoding.UTF8,
                StringEncoding.UTF16LE => Encoding.Unicode,
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode,
                StringEncoding.ASCII => Encoding.ASCII,
                StringEncoding.UTF32 => Encoding.UTF32,
#if NET6_0_OR_GREATER
            StringEncoding.Latin1 => Encoding.Latin1,
#endif
#pragma warning disable 0618, SYSLIB0001
                StringEncoding.UTF7 => Encoding.UTF7,
#pragma warning restore 0618, SYSLIB0001
                _ => throw new ArgumentOutOfRangeException(nameof(encoding))
            };
        }

        /// <summary>
        /// Base56 encoding of the bytes.<br />
        /// 将字节数组进行 Base56 编码。
        /// </summary>
        /// <param name="input">The bytes to be converted.<br />需要转换的字节数组</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static unsafe string Encode(byte[] input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length == 0) return string.Empty;

            int leadingZeros = CountLeadingZeros(input);
            int dataLength = input.Length - leadingZeros;
            if (dataLength == 0) return new string(Base56Chars[0], leadingZeros);

            fixed (byte* pInput = input)
            {
                int outputSize = (int)(dataLength * 1.4) + 1;
                char* outputBuffer = stackalloc char[outputSize];
                int outputIndex = outputSize;

                int bufferSize = dataLength * 2;
                int* buffer = stackalloc int[bufferSize];
                int bufferIndex = 0;

                for (int i = leadingZeros; i < input.Length; i++)
                {
                    int carry = pInput[i];
                    for (int j = 0; j < bufferIndex; j++)
                    {
                        carry += buffer[j] << 8;
                        buffer[j] = carry % 56;
                        carry /= 56;
                    }

                    while (carry > 0)
                    {
                        buffer[bufferIndex++] = carry % 56;
                        carry /= 56;
                    }
                }

                int totalLength = leadingZeros + bufferIndex;
                char* result = stackalloc char[totalLength];
                int resultIndex = 0;

                for (int i = 0; i < leadingZeros; i++)
                {
                    result[resultIndex++] = Base56Chars[0];
                }

                for (int i = bufferIndex - 1; i >= 0; i--)
                {
                    result[resultIndex++] = Base56Chars[buffer[i]];
                }

                return new string(result, 0, totalLength);
            }
        }

        /// <summary>
        /// Base56 decoding of the bytes.<br />
        /// 将字节数组进行 Base56 解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <returns>The decoded bytes.<br />被解码的字节数组</returns>
        public static unsafe byte[] Decode(string input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length == 0) return Array.Empty<byte>();

            int leadingZeros = 0;
            while (leadingZeros < input.Length && input[leadingZeros] == Base56Chars[0])
            {
                leadingZeros++;
            }

            fixed (char* pInput = input)
            {
                int bufferSize = (int)(input.Length * 0.73) + 1;
                byte* buffer = stackalloc byte[bufferSize];
                int bufferIndex = 0;

                for (int i = leadingZeros; i < input.Length; i++)
                {
                    char c = pInput[i];
                    int digit = c < 128 ? CharToIndexMap[c] : -1;
                    if (digit == -1) throw new FormatException($"Invalid character '{c}'");

                    int carry = digit;
                    for (int j = 0; j < bufferIndex; j++)
                    {
                        carry += buffer[j] * 56;
                        buffer[j] = (byte)(carry & 0xFF);
                        carry >>= 8;
                    }

                    while (carry > 0)
                    {
                        buffer[bufferIndex++] = (byte)(carry & 0xFF);
                        carry >>= 8;
                    }
                }

                byte[] result = new byte[leadingZeros + bufferIndex];
                for (int i = 0; i < leadingZeros; i++)
                {
                    result[i] = 0;
                }

                for (int i = 0; i < bufferIndex; i++)
                {
                    result[leadingZeros + bufferIndex - 1 - i] = buffer[i];
                }

                return result;
            }
        }

        private static int CountLeadingZeros(byte[] input)
        {
            int count = 0;
            foreach (byte b in input)
            {
                if (b != 0) break;
                count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Static string extension of Base56 codec library.<br />
    /// Base56 编解码库的静态字符串拓展。
    /// </summary>
    public static class Base56Extension
    {
        /// <summary>
        /// Base56 encoding of the string.<br />
        /// 将字符串进行 Base56 编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string EncodeBase56(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base56.EncodeString(input, encoding);

        /// <summary>
        /// Base56 decoding of the string.<br />
        /// 将字符串进行 Base56 解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string DecodeBase56(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base56.DecodeString(input, encoding);

        /// <summary>
        /// Base56 encoding of the bytes.<br />
        /// 将字节数组进行 Base56 编码。
        /// </summary>
        /// <param name="input">The bytes to be converted.<br />需要转换的字节数组</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string EncodeBase56(this byte[] input) => Base56.Encode(input);

        /// <summary>
        /// Base56 decoding of the bytes.<br />
        /// 将字节数组进行 Base56 解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <returns>The decoded bytes.<br />被解码的字节数组</returns>
        public static byte[] DecodeBase56(this string input) => Base56.Decode(input);
    }
}
#endif