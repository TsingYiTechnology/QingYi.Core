using System.Text;
using System;

namespace QingYi.Core.String.Base
{
    /// <summary>
    /// Base58 codec library.<br />
    /// Base58 编解码库。
    /// </summary>
    public unsafe class Base58
    {
        private const string Base58Chars = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        private static readonly int[] Alphabet = new int[256];
        private static readonly bool AlphabetInitialized;

        static Base58()
        {
            if (AlphabetInitialized) return;

            for (int i = 0; i < Alphabet.Length; i++)
                Alphabet[i] = -1;

            for (int i = 0; i < Base58Chars.Length; i++)
                Alphabet[Base58Chars[i]] = i;

            AlphabetInitialized = true;
        }

        /// <summary>
        /// Gets the base58-encoded character set.<br />
        /// 获取 Base58 编码的字符集。
        /// </summary>
        /// <returns>The base58-encoded character set.<br />Base58 编码的字符集</returns>
        public override string ToString() => Base58Chars;

        /// <summary>
        /// Base58 encoding of the string.<br />
        /// 将字符串进行Base58编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string Encode(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var bytes = GetBytes(input, encoding);
            return Encode(bytes);
        }

        /// <summary>
        /// Base58 decoding of the string.<br />
        /// 将字符串进行Base58解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string Decode(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var bytes = DecodeToBytes(input);
            return GetString(bytes, encoding);
        }

        internal static unsafe string Encode(byte[] input)
        {
            if (input.Length == 0) return string.Empty;

            int leadingZeros = 0;
            while (leadingZeros < input.Length && input[leadingZeros] == 0)
                leadingZeros++;

            fixed (byte* inputPtr = input)
            {
                int length = input.Length;
                int count = (length - leadingZeros) * 138 / 100 + 1;
                byte[] temp = new byte[count];
                int outputIndex = count;

                fixed (byte* tempPtr = temp)
                {
                    for (int i = leadingZeros; i < length; i++)
                    {
                        int carry = inputPtr[i];
                        int j = count - 1;
                        while (carry != 0 || j >= outputIndex)
                        {
                            carry += tempPtr[j] << 8;
                            tempPtr[j] = (byte)(carry % 58);
                            carry /= 58;
                            j--;
                        }
                        outputIndex = j + 1;
                    }
                }

                int startIndex = outputIndex;
                while (startIndex < count && temp[startIndex] == 0)
                    startIndex++;

                char* resultPtr = stackalloc char[leadingZeros + (count - startIndex)];
                for (int i = 0; i < leadingZeros; i++)
                    resultPtr[i] = '1';

                for (int i = leadingZeros; i < leadingZeros + count - startIndex; i++)
                    resultPtr[i] = Base58Chars[temp[startIndex + i - leadingZeros]];

                return new string(resultPtr, 0, leadingZeros + count - startIndex);
            }
        }

        internal static unsafe byte[] DecodeToBytes(string input)
        {
            if (input.Length == 0) return Array.Empty<byte>();

            int leadingOnes = 0;
            while (leadingOnes < input.Length && input[leadingOnes] == '1')
                leadingOnes++;

            fixed (char* inputPtr = input)
            {
                int length = input.Length;
                byte[] indices = new byte[length - leadingOnes];

                for (int i = leadingOnes; i < length; i++)
                {
                    char c = inputPtr[i];
                    int value = c < 0 || c >= Alphabet.Length ? -1 : Alphabet[c];
                    if (value == -1)
                        throw new FormatException($"Invalid Base58 character '{c}'");
                    indices[i - leadingOnes] = (byte)value;
                }

                int count = (length - leadingOnes) * 733 / 1000 + 1;
                byte[] temp = new byte[count];
                int outputIndex = count;

                fixed (byte* indicesPtr = indices, tempPtr = temp)
                {
                    for (int i = 0; i < indices.Length; i++)
                    {
                        int carry = indicesPtr[i];
                        int j = count - 1;
                        while (carry != 0 || j >= outputIndex)
                        {
                            carry += tempPtr[j] * 58;
                            tempPtr[j] = (byte)(carry & 0xFF);
                            carry >>= 8;
                            j--;
                        }
                        outputIndex = j + 1;
                    }
                }

                int startIndex = outputIndex;
                while (startIndex < count && temp[startIndex] == 0)
                    startIndex++;

                byte[] result = new byte[leadingOnes + (count - startIndex)];
                for (int i = 0; i < leadingOnes; i++)
                    result[i] = 0;

                Buffer.BlockCopy(temp, startIndex, result, leadingOnes, count - startIndex);
                return result;
            }
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
                _ => throw new NotSupportedException($"Encoding {encoding} is not supported")
            };
        }

        private static byte[] GetBytes(string input, StringEncoding encoding) => GetEncoding(encoding).GetBytes(input);

        private static string GetString(byte[] bytes, StringEncoding encoding) => GetEncoding(encoding).GetString(bytes);
    }

    /// <summary>
    /// Static string extension of Base62 codec library.<br />
    /// Base58 编解码库的静态字符串拓展。
    /// </summary>
    public static class Base58Extension
    {
        /// <summary>
        /// Base58 encoding of the string.<br />
        /// 将字符串进行Base58编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string Encode(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base58.Encode(input, encoding);

        /// <summary>
        /// Base58 decoding of the string.<br />
        /// 将字符串进行Base58解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string Decode(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base58.Decode(input, encoding);

        /// <summary>
        /// Base58 encoding of the bytes.<br />
        /// 将字节数组进行Base58编码。
        /// </summary>
        /// <param name="input">The bytes to be converted.<br />需要转换的字节数组</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string Encode(this byte[] input) => Base58.Encode(input);

        /// <summary>
        /// Base58 decoding of the string.<br />
        /// 将字符串进行Base58解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <returns>The decoded bytes.<br />被解码的字节数组</returns>
        public static byte[] Decode(this string input) => Base58.DecodeToBytes(input);
    }
}
