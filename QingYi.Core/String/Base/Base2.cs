using System.Text;
using System;

#pragma warning disable SYSLIB0001, CS0618, CA1510

namespace QingYi.Core.String.Base
{
    /// <summary>
    /// Base 2 编解码库。<br />
    /// Base 2 codec library.
    /// </summary>
    public static class Base2
    {
        private static readonly char[] PrecomputedBits = new char[256 * 8];
        private static readonly byte[] BitMasks = new byte[8] { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };

        static Base2()
        {
            // 初始化预计算位
            for (int i = 0; i < 256; i++)
            {
                byte b = (byte)i;
                for (int j = 0; j < 8; j++)
                {
                    PrecomputedBits[i * 8 + j] = (b & (0x80 >> j)) != 0 ? '1' : '0';
                }
            }
        }

        /// <summary>
        /// 将字符串编码为 Base 2，默认为小端。<br />
        /// Encodes the string as Base 2, which defaults to a small endian.
        /// </summary>
        /// <param name="input">输入文本<br />Input text</param>
        /// <param name="encoding">字符串编码格式<br />String encoding format</param>
        /// <param name="isBigEndian">启用大端<br />Enable big endian</param>
        /// <returns>被编码的字符串<br />Encoded string</returns>
        /// <exception cref="ArgumentNullException">输入字符串为Null<br />The input string is Null</exception>
        public static string Encode(string input, StringEncoding encoding, bool isBigEndian = false)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            byte[] bytes = GetEncodedBytes(input, encoding, isBigEndian);
            return BytesToBase2(bytes);
        }
        
        /// <summary>
        /// 将字符串编码为 Base 2，默认为小端。<br />
        /// Encodes the string as Base 2, which defaults to a small endian.
        /// </summary>
        /// <param name="input">输入文本<br />Input text</param>
        /// <param name="isBigEndian">启用大端<br />Enable big endian</param>
        /// <returns>被编码的字符串<br />Encoded string</returns>
        public static string Encode(string input, bool isBigEndian = false) => Encode(input, StringEncoding.UTF8, isBigEndian);

        /// <summary>
        /// 将字符串解码为 Base 2，默认为小端。<br />
        /// Decodes the string as Base 2, which defaults to a small endian.
        /// </summary>
        /// <param name="base2">输入文本<br />Input base 2 text</param>
        /// <param name="encoding">字符串编码格式<br />String encoding format</param>
        /// <param name="isBigEndian">启用大端<br />Enable big endian</param>
        /// <returns>被解码的字符串<br />Decoded string</returns>
        /// <exception cref="ArgumentNullException">输入字符串为Null<br />The input string is Null</exception>
        public static string Decode(string base2, StringEncoding encoding, bool isBigEndian = false)
        {
            if (base2 == null) throw new ArgumentNullException(nameof(base2));

            byte[] bytes = Base2ToBytes(base2);
            return GetDecodedString(bytes, encoding, isBigEndian);
        }

        /// <summary>
        /// 将字符串解码为 Base 2，默认为小端。<br />
        /// Decodes the string as Base 2, which defaults to a small endian.
        /// </summary>
        /// <param name="base2">输入文本<br />Input base 2 text</param>
        /// <param name="isBigEndian">启用大端<br />Enable big endian</param>
        /// <returns>被解码的字符串<br />Decoded string</returns>
        public static string Decode(string base2, bool isBigEndian = false)
        {
            return Decode(base2, StringEncoding.UTF8, isBigEndian);
        }

        private static byte[] GetEncodedBytes(string input, StringEncoding encoding, bool isBigEndian)
        {
            return encoding switch
            {
                StringEncoding.UTF8 => Encoding.UTF8.GetBytes(input),
                StringEncoding.UTF16LE => Encoding.Unicode.GetBytes(input),
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetBytes(input),
                StringEncoding.UTF32 => GetUtf32Bytes(input, isBigEndian),
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1.GetBytes(input),
#endif
                StringEncoding.ASCII => Encoding.ASCII.GetBytes(input),
                StringEncoding.UTF7 => Encoding.UTF7.GetBytes(input),
                _ => throw new ArgumentOutOfRangeException(nameof(encoding))
            };
        }

        private static string GetDecodedString(byte[] bytes, StringEncoding encoding, bool isBigEndian)
        {
            if (bytes.Length == 0) return string.Empty;

            return encoding switch
            {
                StringEncoding.UTF8 => Encoding.UTF8.GetString(bytes),
                StringEncoding.UTF16LE => Encoding.Unicode.GetString(bytes),
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetString(bytes),
                StringEncoding.UTF32 => GetUtf32String(bytes, isBigEndian),
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1.GetString(bytes),
#endif
                StringEncoding.ASCII => Encoding.ASCII.GetString(bytes),
                StringEncoding.UTF7 => Encoding.UTF7.GetString(bytes),
                _ => throw new ArgumentOutOfRangeException(nameof(encoding))
            };
        }

        private static byte[] GetUtf32Bytes(string input, bool isBigEndian)
        {
            byte[] bytes = Encoding.UTF32.GetBytes(input);
            if (isBigEndian != BitConverter.IsLittleEndian)
            {
                SwapEndianness(bytes, 4);
            }
            return bytes;
        }

        private static string GetUtf32String(byte[] bytes, bool isBigEndian)
        {
            if (isBigEndian != BitConverter.IsLittleEndian)
            {
                byte[] copy = new byte[bytes.Length];
                Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
                SwapEndianness(copy, 4);
                return Encoding.UTF32.GetString(copy);
            }
            return Encoding.UTF32.GetString(bytes);
        }

        private static unsafe void SwapEndianness(byte[] bytes, int elementSize)
        {
            fixed (byte* ptr = bytes)
            {
                byte* end = ptr + bytes.Length;
                for (byte* p = ptr; p < end; p += elementSize)
                {
                    for (int i = 0; i < elementSize / 2; i++)
                    {
                        (p[elementSize - 1 - i], p[i]) = (p[i], p[elementSize - 1 - i]);
                    }
                }
            }
        }

        private static unsafe string BytesToBase2(byte[] bytes)
        {
            int byteCount = bytes.Length;
            char[] buffer = new char[byteCount * 8];

            fixed (byte* srcPtr = bytes)
            fixed (char* bufferPtr = buffer)
            fixed (char* precomputedPtr = PrecomputedBits)
            {
                byte* src = srcPtr;
                char* dest = bufferPtr;

                for (int i = 0; i < byteCount; i++)
                {
                    char* precomputed = precomputedPtr + (*src++) * 8;
                    Buffer.MemoryCopy(precomputed, dest, 16, 16); // 复制16字节（8个char）
                    dest += 8;
                }
            }

            return new string(buffer);
        }

        private static unsafe byte[] Base2ToBytes(string base2)
        {
            if (base2.Length % 8 != 0)
                throw new ArgumentException("Invalid Base2 string length");

            int byteCount = base2.Length / 8;
            byte[] result = new byte[byteCount];

            fixed (char* inputPtr = base2)
            fixed (byte* resultPtr = result)
            {
                char* src = inputPtr;
                byte* dest = resultPtr;

                for (int i = 0; i < byteCount; i++)
                {
                    byte value = 0;
                    for (int j = 0; j < 8; j++)
                    {
                        char c = *src++;
                        if (c != '0' && c != '1')
                            throw new ArgumentException($"Invalid character '{c}' in Base2 string");

                        value |= (byte)((c - '0') << (7 - j));
                    }
                    *dest++ = value;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Static string extension of Base2 codec library.<br />
    /// Base2 编解码库的静态字符串拓展。
    /// </summary>
    public static class Base2Extension
    {
        /// <summary>
        /// 将字符串编码为 Base 2，默认为小端。<br />
        /// Encodes the string as Base 2, which defaults to a small endian.
        /// </summary>
        /// <param name="input">输入文本<br />Input text</param>
        /// <param name="encoding">字符串编码格式<br />String encoding format</param>
        /// <param name="isBigEndian">启用大端<br />Enable big endian</param>
        /// <returns>被编码的字符串<br />Encoded string</returns>
        public static string EncodeBase2(this string input, StringEncoding encoding, bool isBigEndian = false) => Base2.Encode(input, encoding, isBigEndian);

        /// <summary>
        /// 将字符串编码为 Base 2，默认为小端。<br />
        /// Encodes the string as Base 2, which defaults to a small endian.
        /// </summary>
        /// <param name="input">输入文本<br />Input text</param>
        /// <param name="isBigEndian">启用大端<br />Enable big endian</param>
        /// <returns>被编码的字符串<br />Encoded string</returns>
        public static string EncodeBase2(this string input, bool isBigEndian = false) => Base2.Encode(input, isBigEndian);

        /// <summary>
        /// 将字符串解码为 Base 2，默认为小端。<br />
        /// Decodes the string as Base 2, which defaults to a small endian.
        /// </summary>
        /// <param name="input">输入文本<br />Input base 2 text</param>
        /// <param name="encoding">字符串编码格式<br />String encoding format</param>
        /// <param name="isBigEndian">启用大端<br />Enable big endian</param>
        /// <returns>被解码的字符串<br />Decoded string</returns>
        public static string DecodeBase2(this string input, StringEncoding encoding, bool isBigEndian = false) => Base2.Decode(input, encoding, isBigEndian);

        /// <summary>
        /// 将字符串解码为 Base 2，默认为小端。<br />
        /// Decodes the string as Base 2, which defaults to a small endian.
        /// </summary>
        /// <param name="input">输入文本<br />Input base 2 text</param>
        /// <param name="isBigEndian">启用大端<br />Enable big endian</param>
        /// <returns>被解码的字符串<br />Decoded string</returns>
        public static string DecodeBase2(this string input, bool isBigEndian = false) => Base2.Decode(input, isBigEndian);
    }
}
