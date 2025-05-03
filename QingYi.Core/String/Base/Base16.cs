using System.Text;
using System;

namespace QingYi.Core.String.Base
{
    /// <summary>
    /// Base16 codec library.<br />
    /// Base16 编解码库。
    /// </summary>
    public class Base16
    {
        private static readonly uint[] Lookup32Lower = CreateLookup32('x');
        private static readonly uint[] Lookup32Upper = CreateLookup32('X');
        private static readonly byte[] LookupHex = CreateHexLookup();

        private static uint[] CreateLookup32(char format)
        {
            var result = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                string s = i.ToString(format + "2");
                result[i] = ((uint)s[0]) + ((uint)s[1] << 16);
            }
            return result;
        }

        private static byte[] CreateHexLookup()
        {
            var result = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                if (i >= '0' && i <= '9')
                    result[i] = (byte)(i - '0');
                else if (i >= 'A' && i <= 'F')
                    result[i] = (byte)(i - 'A' + 10);
                else if (i >= 'a' && i <= 'f')
                    result[i] = (byte)(i - 'a' + 10);
                else
                    result[i] = 0xFF;
            }
            return result;
        }

        /// <summary>
        /// Base16 encoding of the string.<br />
        /// 将字符串进行Base16编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string Encode(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            byte[] bytes = GetBytes(input, encoding);
            return Encode(bytes);
        }

        /// <summary>
        /// Base16 encoding of the bytes.<br />
        /// 将字节数组进行Base16编码。
        /// </summary>
        /// <param name="bytes">The bytes to be converted.<br />需要转换的字节数组</param>
        /// <param name="lowerCase">Use lowercase letters<br />使用小写字母</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string Encode(byte[] bytes, bool lowerCase = false)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length == 0) return string.Empty;

            string result = new string('\0', bytes.Length * 2);
            unsafe
            {
                fixed (byte* bytesPtr = bytes)
                fixed (char* resultPtr = result)
                fixed (uint* lowerPtr = Lookup32Lower)
                fixed (uint* upperPtr = Lookup32Upper)
                {
                    uint* lookup = lowerCase ? lowerPtr : upperPtr;
                    uint* resultUIntPtr = (uint*)resultPtr;
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        resultUIntPtr[i] = lookup[bytesPtr[i]];
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Base16 decoding of the string.<br />
        /// 将字符串进行Base16解码。
        /// </summary>
        /// <param name="base16String">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string Decode(string base16String, StringEncoding encoding = StringEncoding.UTF8) => GetString(Decode(base16String), encoding);

        /// <summary>
        /// Base16 decoding of the string.<br />
        /// 将字符串进行Base16解码。
        /// </summary>
        /// <param name="base16String">The string to be converted.<br />需要转换的字符串</param>
        /// <returns>The decoded bytes.<br />被解码的字节数组</returns>
        public static byte[] Decode(string base16String)
        {
            if (base16String == null) throw new ArgumentNullException(nameof(base16String));
            if (base16String.Length % 2 != 0)
                throw new ArgumentException("Base16 string length must be even.");

            if (base16String.Length == 0) return Array.Empty<byte>();

            byte[] result = new byte[base16String.Length / 2];
            unsafe
            {
                fixed (char* inputPtr = base16String)
                fixed (byte* resultPtr = result)
                {
                    byte* currentResult = resultPtr;
                    for (int i = 0; i < base16String.Length; i += 2)
                    {
                        char c1 = inputPtr[i];
                        char c2 = inputPtr[i + 1];

                        if (c1 > 255 || c2 > 255)
                            ThrowInvalidCharacter();

                        byte b1 = LookupHex[(byte)c1];
                        byte b2 = LookupHex[(byte)c2];

                        if (b1 == 0xFF || b2 == 0xFF)
                            ThrowInvalidCharacter();

                        *currentResult++ = (byte)((b1 << 4) | b2);
                    }
                }
            }
            return result;
        }

        private static void ThrowInvalidCharacter() => throw new ArgumentException("Invalid Base16 character.");

        private static byte[] GetBytes(string input, StringEncoding encoding) => GetEncoding(encoding).GetBytes(input);

        private static string GetString(byte[] bytes, StringEncoding encoding) => GetEncoding(encoding).GetString(bytes);

        private static Encoding GetEncoding(StringEncoding encoding)
        {
            switch (encoding)
            {
                case StringEncoding.UTF8: return Encoding.UTF8;
                case StringEncoding.UTF16LE: return Encoding.Unicode;
                case StringEncoding.UTF16BE: return Encoding.BigEndianUnicode;
                case StringEncoding.ASCII: return Encoding.ASCII;
                case StringEncoding.UTF32: return Encoding.UTF32;
#if NET6_0_OR_GREATER
            case StringEncoding.Latin1: return Encoding.Latin1;
#endif
#pragma warning disable CS0618, SYSLIB0001
                case StringEncoding.UTF7: return Encoding.UTF7;
#pragma warning restore CS0618, SYSLIB0001
                default: throw new NotSupportedException($"Encoding {encoding} is not supported.");
            }
        }
    }

    /// <summary>
    /// Static string extension of Base16 codec library.<br />
    /// Base16 编解码库的静态字符串拓展。
    /// </summary>
    public static class Base16Extension
    {
        /// <summary>
        /// Base16 encoding of the string.<br />
        /// 将字符串进行Base16编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string EncodeBase16(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base16.Encode(input, encoding);

        /// <summary>
        /// Base16 encoding of the bytes.<br />
        /// 将字节数组进行Base16编码。
        /// </summary>
        /// <param name="bytes">The bytes to be converted.<br />需要转换的字节数组</param>
        /// <param name="lowerCase">Use lowercase letters<br />使用小写字母</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string EncodeBase16(byte[] bytes, bool lowerCase = false) => Base16.Encode(bytes, lowerCase);

        /// <summary>
        /// Base16 decoding of the string.<br />
        /// 将字符串进行Base16解码。
        /// </summary>
        /// <param name="base16String">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string DecodeBase16(this string base16String, StringEncoding encoding = StringEncoding.UTF8) => Base16.Decode(base16String, encoding);

        /// <summary>
        /// Base16 decoding of the string.<br />
        /// 将字符串进行Base16解码。
        /// </summary>
        /// <param name="base16String">The string to be converted.<br />需要转换的字符串</param>
        /// <returns>The decoded bytes.<br />被解码的字节数组</returns>
        public static byte[] DecodeBase16(this string base16String) => Base16.Decode(base16String);
    }
}
