using System;
using System.Text;

namespace QingYi.Core.String.Base
{
    /// <summary>
    /// Base10 codec library.<br />
    /// Base10 编解码库。
    /// </summary>
    public class Base10
    {
        private static readonly char[] s_digits100 = new char[256];
        private static readonly char[] s_digits10 = new char[256];
        private static readonly char[] s_digits1 = new char[256];

        static Base10()
        {
            for (int i = 0; i < 256; i++)
            {
                s_digits100[i] = (char)('0' + i / 100);
                s_digits10[i] = (char)('0' + (i % 100) / 10);
                s_digits1[i] = (char)('0' + i % 10);
            }
        }

        /// <summary>
        /// Base10 encoding of the string.<br />
        /// 将字符串进行Base10编码。
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
        /// Base10 decoding of the string.<br />
        /// 将字符串进行Base10解码。
        /// </summary>
        /// <param name="base10String">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string Decode(string base10String, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (base10String == null) throw new ArgumentNullException(nameof(base10String));

            byte[] bytes = DecodeToBytes(base10String);
            return GetEncoding(encoding).GetString(bytes);
        }

        private static string EncodeBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;

            int length = bytes.Length;
            char[] result = new char[length * 3];

            unsafe
            {
                fixed (byte* bytesPtr = bytes)
                fixed (char* resultPtr = result)
                {
                    byte* src = bytesPtr;
                    char* dest = resultPtr;

                    for (int i = 0; i < length; i++)
                    {
                        byte b = *src++;
                        *dest++ = s_digits100[b];
                        *dest++ = s_digits10[b];
                        *dest++ = s_digits1[b];
                    }
                }
            }

            return new string(result);
        }

        private static byte[] DecodeToBytes(string base10String)
        {
            if (base10String.Length % 3 != 0)
                throw new ArgumentException("Invalid Base10 string length", nameof(base10String));

            int outputLength = base10String.Length / 3;
            byte[] result = new byte[outputLength];

            unsafe
            {
                fixed (char* inputPtr = base10String)
                fixed (byte* outputPtr = result)
                {
                    char* src = inputPtr;
                    byte* dest = outputPtr;

                    for (int i = 0; i < outputLength; i++)
                    {
                        int num = 0;

                        for (int j = 0; j < 3; j++)
                        {
                            char c = *src++;
                            if (c < '0' || c > '9')
                                throw new FormatException("Invalid character in Base10 string");

                            num = num * 10 + (c - '0');
                        }

                        if (num > 255)
                            throw new FormatException("Value exceeds byte range");

                        *dest++ = (byte)num;
                    }
                }
            }

            return result;
        }

        private static Encoding GetEncoding(StringEncoding encoding)
        {
            return encoding switch
            {
                StringEncoding.UTF8 => Encoding.UTF8,
                StringEncoding.UTF16LE => new UnicodeEncoding(false, false),
                StringEncoding.UTF16BE => new UnicodeEncoding(true, false),
                StringEncoding.ASCII => Encoding.ASCII,
                StringEncoding.UTF32 => new UTF32Encoding(false, false),
#if NET6_0_OR_GREATER
            StringEncoding.Latin1 => Encoding.Latin1,
#endif
#pragma warning disable CS0618, SYSLIB0001
                StringEncoding.UTF7 => Encoding.UTF7,
#pragma warning restore CS0618, SYSLIB0001
                _ => throw new ArgumentOutOfRangeException(nameof(encoding))
            };
        }
    }

    /// <summary>
    /// Static string extension of Base10 codec library.<br />
    /// Base10 编解码库的静态字符串拓展。
    /// </summary>
    public static class Base10Extension
    {
        /// <summary>
        /// Base10 encoding of the string.<br />
        /// 将字符串进行 Base10 编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string EncodeBase10(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base10.Encode(input, encoding);

        /// <summary>
        /// Base10 decoding of the string.<br />
        /// 将字符串进行 Base10 解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string DecodeBase10(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base10.Decode(input, encoding);
    }
}
