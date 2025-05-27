using System;

namespace QingYi.Core.String.Base
{
    /// <summary>
    /// Base32 codec library (Default alphabet is RFC4648).<br />
    /// Base32 编解码库（默认字符集为RFC4648）。
    /// </summary>
    public class Base32
    {
        /// <summary>
        /// Base36 encoding of the string.<br />
        /// 将字符串进行Base32编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string Encode(string input, StringEncoding encoding = StringEncoding.UTF8) => Base32RFC4648.Encode(input, encoding);

        /// <summary>
        /// Base36 decoding of the string.<br />
        /// 将字符串进行Base32解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string Decode(string input, StringEncoding encoding = StringEncoding.UTF8) => Base32RFC4648.Decode(input, encoding);

        /// <summary>
        /// Gets the base32-encoded character set.<br />
        /// 获取 Base32 编码的字符集。
        /// </summary>
        /// <returns>The base32-encoded character set.<br />Base32 编码的字符集</returns>
        public override string ToString() => "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        /// <summary>
        /// Base32 alphabet.<br />
        /// Base32 字符集。
        /// </summary>
        [Flags]
        public enum Alphabet
        {
            /// <summary>
            /// RFC 4648 Base32 alphabet, commonly used in various applications.
            /// <br />
            /// RFC 4648 Base32字符集，广泛应用于各种程序。
            /// </summary>
            RFC4648,

            /// <summary>
            /// Crockford Base32 alphabet, typically used in encoding and identification systems.
            /// <br />
            /// Crockford Base32字符集，通常用于编码和标识系统。
            /// </summary>
            Crockford,

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            /// <summary>
            /// Extended Hex Base32 alphabet, which uses the digits 0-9 and letters A-V.<br />
            /// 扩展十六进制Base32字符集，使用数字0-9和字母A-V。
            /// </summary>
            ExtendHex,

            /// <summary>
            /// GeoHash Base32 alphabet, often used in geographic data encoding.<br />
            /// GeoHash Base32字符集，通常用于地理数据编码。
            /// </summary>
            GeoHash,

            /// <summary>
            /// WordSafe Base32 alphabet, designed to avoid using similar looking characters.<br />
            /// WordSafe Base32字符集，设计用于避免使用外观相似的字符。
            /// </summary>
            WordSafe,
#endif

            /// <summary>
            /// zBase32 alphabet, a variation of Base32 optimized for use in URLs.<br />
            /// zBase32字符集，是一种为URL使用优化的Base32变体。
            /// </summary>
            zBase32,
        }
    }

    /// <summary>
    /// Static string extension of Base32 codec library.<br />
    /// Base32 编解码库的静态字符串拓展。
    /// </summary>
    public static class Base32Extension
    {
        /// <summary>
        /// Base36 encoding of the string.<br />
        /// 将字符串进行Base32编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="alphabet">Base32 alphabet.<br />Base32 字符集</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string EncodeBase32(this string input, Base32.Alphabet alphabet = Base32.Alphabet.RFC4648, StringEncoding encoding = StringEncoding.UTF8)
        {
            switch (alphabet)
            {
                case Base32.Alphabet.Crockford:
                    return Base32Crockford.Encode(input, encoding);
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                case Base32.Alphabet.ExtendHex:
                    return Base32ExtendedHex.Encode(input, encoding);
                case Base32.Alphabet.GeoHash:
                    return Base32GeoHash.Encode(input, encoding);
                case Base32.Alphabet.WordSafe:
                    return Base32WordSafe.Encode(input, encoding);
#endif
                case Base32.Alphabet.zBase32:
                    return Base32z.Encode(input, encoding);
                case Base32.Alphabet.RFC4648:
                default:
                    return Base32.Encode(input, encoding);
            }
        }

        /// <summary>
        /// Base36 decoding of the string.<br />
        /// 将字符串进行Base32解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="alphabet">Base32 alphabet.<br />Base32 字符集</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string DecodeBase32(this string input, Base32.Alphabet alphabet = Base32.Alphabet.RFC4648, StringEncoding encoding = StringEncoding.UTF8)
        {
            switch (alphabet)
            {
                case Base32.Alphabet.Crockford:
                    return Base32Crockford.Decode(input, encoding);
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                case Base32.Alphabet.ExtendHex:
                    return Base32ExtendedHex.Decode(input, encoding);
                case Base32.Alphabet.GeoHash:
                    return Base32GeoHash.Decode(input, encoding);
                case Base32.Alphabet.WordSafe:
                    return Base32WordSafe.Decode(input, encoding);
#endif
                case Base32.Alphabet.zBase32:
                    return Base32z.Decode(input, encoding);
                case Base32.Alphabet.RFC4648:
                default:
                    return Base32.Decode(input, encoding);
            }
        }
    }
}
