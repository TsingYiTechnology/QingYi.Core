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
#pragma warning disable CS0618, SYSLIB0001
                    case StringEncoding.UTF7:
                        encoder = Encoding.UTF7;
                        break;
#pragma warning restore CS0618, SYSLIB0001
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
                return encoder.GetBytes(s);
            }

            public static string GetString(byte[] bytes, StringEncoding encoding)
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
#pragma warning disable CS0618, SYSLIB0001
                    case StringEncoding.UTF7:
                        decoder = Encoding.UTF7;
                        break;
#pragma warning restore CS0618, SYSLIB0001
                    default:
                        throw new ArgumentOutOfRangeException(nameof(encoding));
                }
                return decoder.GetString(bytes);
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
