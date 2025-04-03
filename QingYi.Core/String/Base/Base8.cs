using System;
using System.Text;

namespace QingYi.Core.String.Base
{
    /// <summary>
    /// Base 8 编解码库。<br />
    /// Base 8 codec library.
    /// </summary>
    public class Base8
    {
        /// <summary>
        /// Base8 encoding of the byte array.<br />
        /// 将字节数组进行Base8编码。
        /// </summary>
        /// <param name="data">An array of bytes to be encoded.<br />要编码的字节数组</param>
        /// <returns>An array of bytes to be encoded.<br />被编码的字节数组</returns>
        public static unsafe string Encode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) return string.Empty;

            int length = data.Length;
            char[] result = new char[length * 3];

            fixed (byte* pData = data)
            fixed (char* pResult = result)
            {
                byte* src = pData;
                char* dest = pResult;

                for (int i = 0; i < length; i++)
                {
                    byte b = *src++;
                    *dest++ = (char)('0' + (b >> 6));
                    *dest++ = (char)('0' + ((b >> 3) & 0x07));
                    *dest++ = (char)('0' + (b & 0x07));
                }
            }

            return new string(result);
        }

        /// <summary>
        /// Base8 decoding of the byte array.<br />
        /// 将字节数组进行Base8解码。
        /// </summary>
        /// <param name="base8">The string to be converted.<br />需要转换的字符串</param>
        /// <returns>An array of bytes to be decoded.<br />被解码的字符串</returns>
        public static unsafe byte[] Decode(string base8)
        {
            if (base8 == null) throw new ArgumentNullException(nameof(base8));
            if (base8.Length % 3 != 0) throw new ArgumentException("Invalid Base8 string length");

            int byteCount = base8.Length / 3;
            if (byteCount == 0) return Array.Empty<byte>();

            byte[] result = new byte[byteCount];

            fixed (char* pBase8 = base8)
            fixed (byte* pResult = result)
            {
                char* src = pBase8;
                byte* dest = pResult;

                for (int i = 0; i < byteCount; i++)
                {
                    byte b = 0;
                    for (int j = 0; j < 3; j++)
                    {
                        int c = *src++;
                        if (c < '0' || c > '7')
                            throw new ArgumentException($"Invalid Base8 character: {(char)c}");

                        b = (byte)((b << 3) | (c - '0'));
                    }
                    *dest++ = b;
                }
            }

            return result;
        }

        /// <summary>
        /// Base8 encoding of the string.<br />
        /// 将字符串进行Base8编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string EncodeString(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            byte[] bytes = StringEncodingHelper.GetBytes(input, encoding);
            return Encode(bytes);
        }

        /// <summary>
        /// Base8 decoding of the string.<br />
        /// 将字符串进行Base8解码。
        /// </summary>
        /// <param name="base8">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string DecodeString(string base8, StringEncoding encoding = StringEncoding.UTF8)
        {
            byte[] bytes = Decode(base8);
            return StringEncodingHelper.GetString(bytes, encoding);
        }

        static class StringEncodingHelper
        {
            public static byte[] GetBytes(string s, StringEncoding encoding)
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
#pragma warning disable CS0618, SYSLIB0001
                    StringEncoding.UTF7 => GetUTF7Bytes(s),
#pragma warning restore CS0618, SYSLIB0001
                    _ => throw new NotSupportedException($"Encoding {encoding} is not supported")
                };
            }

            public static string GetString(byte[] bytes, StringEncoding encoding)
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
#pragma warning disable CS0618, SYSLIB0001
                    StringEncoding.UTF7 => GetUTF7String(bytes),
#pragma warning restore CS0618, SYSLIB0001
                    _ => throw new NotSupportedException($"Encoding {encoding} is not supported")
                };
            }

            private static byte[] GetUTF7Bytes(string s)
            {
#pragma warning disable CS0618, SYSLIB0001
                return Encoding.UTF7.GetBytes(s);
#pragma warning restore CS0618, SYSLIB0001
            }

            private static string GetUTF7String(byte[] bytes)
            {
#pragma warning disable CS0618, SYSLIB0001
                return Encoding.UTF7.GetString(bytes);
#pragma warning restore CS0618, SYSLIB0001
            }
        }
    }

    /// <summary>
    /// Static string extension of Base8 codec library.<br />
    /// Base8 编解码库的静态字符串拓展。
    /// </summary>
    public static class Base8Extension
    {
        /// <summary>
        /// Base8 encoding of the string.<br />
        /// 将字符串进行 Base8 编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string EncodeBase8(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base8.EncodeString(input, encoding);

        /// <summary>
        /// Base8 decoding of the string.<br />
        /// 将字符串进行 Base8 解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string DecodeBase8(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base8.DecodeString(input, encoding);

        /// <summary>
        /// Base8 encoding of the bytes.<br />
        /// 将字节数组进行 Base8 编码。
        /// </summary>
        /// <param name="input">The bytes to be converted.<br />需要转换的字节数组</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string EncodeBase8(this byte[] input) => Base8.Encode(input);

        /// <summary>
        /// Base8 decoding of the bytes.<br />
        /// 将字节数组进行 Base8 解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <returns>The decoded bytes.<br />被解码的字节数组</returns>
        public static byte[] DecodeBase8(this string input) => Base8.Decode(input);
    }
}
