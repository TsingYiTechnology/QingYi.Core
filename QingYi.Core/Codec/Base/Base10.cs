using System;
using System.Text;

namespace QingYi.Core.Codec.Base
{
    /// <summary>
    /// Provides Base10 encoding and decoding functionality.
    /// Base10 represents each byte as three decimal digits (000-255).
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
                s_digits10[i] = (char)('0' + i % 100 / 10);
                s_digits1[i] = (char)('0' + i % 10);
            }
        }

        /// <summary>
        /// Encodes a string into Base10 format.
        /// </summary>
        /// <param name="input">The string to encode.</param>
        /// <param name="encoding">Character encoding to use (default: UTF8).</param>
        /// <returns>Base10 encoded string.</returns>
        /// <exception cref="ArgumentNullException">Thrown if input is null.</exception>
        public static string Encode(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            byte[] bytes = GetEncoding(encoding).GetBytes(input);
            return EncodeBytes(bytes);
        }

        /// <summary>
        /// Encodes a byte array into Base10 format.
        /// </summary>
        /// <param name="input">Byte array to encode.</param>
        /// <returns>Base10 encoded string.</returns>
        /// <exception cref="ArgumentNullException">Thrown if input is null.</exception>
        public static string Encode(byte[] input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            return EncodeBytes(input);
        }

        /// <summary>
        /// Decodes a Base10 string into its original string.
        /// </summary>
        /// <param name="base10String">Base10 encoded string.</param>
        /// <param name="encoding">Character encoding to use (default: UTF8).</param>
        /// <returns>Decoded original string.</returns>
        /// <exception cref="ArgumentNullException">Thrown if input is null.</exception>
        /// <exception cref="ArgumentException">Thrown for invalid input length.</exception>
        /// <exception cref="FormatException">Thrown for invalid characters or values.</exception>
        public static string Decode(string base10String, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (base10String == null) throw new ArgumentNullException(nameof(base10String));

            byte[] bytes = DecodeToBytes(base10String);
            return GetEncoding(encoding).GetString(bytes);
        }

        /// <summary>
        /// Decodes a Base10 string into a byte array.
        /// </summary>
        /// <param name="base10">Base10 encoded string.</param>
        /// <returns>Decoded byte array.</returns>
        /// <exception cref="ArgumentNullException">Thrown if input is null.</exception>
        /// <exception cref="ArgumentException">Thrown for invalid input length.</exception>
        /// <exception cref="FormatException">Thrown for invalid characters or values.</exception>
        public static byte[] Decode(string base10)
        {
            if (base10 == null) throw new ArgumentNullException(nameof(base10));

            byte[] bytes = DecodeToBytes(base10);
            return bytes;
        }

        /// <summary>
        /// Encodes various data types into Base10 format.
        /// </summary>
        /// <param name="input">
        /// Supported types: 
        /// string, byte[], int, long, float, double, short, ushort, uint, ulong, decimal
        /// </param>
        /// <returns>Base10 encoded string.</returns>
        /// <exception cref="ArgumentException">Thrown for unsupported types.</exception>
        public static object Encode(object input)
        {
            switch (input)
            {
                case null:
                    return null;
                case string _:
                    return Encode((string)input);
                case byte[] _:
                    return Encode((byte[])input);
                case int _:
                    return Encode(BitConverter.GetBytes((int)input));
                case long _:
                    return Encode(BitConverter.GetBytes((long)input));
                case float _:
                    return Encode(BitConverter.GetBytes((float)input));
                case double _:
                    return Encode(BitConverter.GetBytes((double)input));
                case short _:
                    return Encode(BitConverter.GetBytes((short)input));
                case ushort _:
                    return Encode(BitConverter.GetBytes((ushort)input));
                case uint _:
                    return Encode(BitConverter.GetBytes((uint)input));
                case ulong _:
                    return Encode(BitConverter.GetBytes((ulong)input));
                case decimal _:
                    byte[] decimalBytes = new byte[16];
                    decimal.GetBits((decimal)input).CopyTo(decimalBytes, 0);
                    return Encode(decimalBytes);

                default:
                    throw new ArgumentException("This type is not supported temporarily.");
            }
        }

        /// <summary>
        /// Decodes various data types from Base10 format.
        /// </summary>
        /// <param name="input">
        /// Supported types: 
        /// string (Base10 encoded), int, long, short, ushort, uint, ulong
        /// </param>
        /// <returns>
        /// For string: decoded string (UTF8)
        /// For numeric types: decoded byte array
        /// </returns>
        /// <exception cref="ArgumentException">Thrown for unsupported types.</exception>
        public static object Decode(object input)
        {
            switch (input)
            {
                case null:
                    return null;
                case string _:
                    return Decode((string)input);
                case int _:
                    return Decode(Convert.ToString((int)input));
                case long _:
                    return Decode(Convert.ToString((long)input));
                case short _:
                    return Decode(Convert.ToString((short)input));
                case ushort _:
                    return Decode(Convert.ToString((ushort)input));
                case uint _:
                    return Decode(Convert.ToString((uint)input));
                case ulong _:
                    return Decode(Convert.ToString((ulong)input));

                default:
                    throw new ArgumentException("This type is not supported temporarily.");
            }
        }

        /// <summary>
        /// Efficiently encodes byte array to Base10 using precomputed digits.
        /// </summary>
        /// <param name="bytes">Byte array to encode.</param>
        /// <returns>Base10 encoded string.</returns>
        public static string EncodeBytes(byte[] bytes)
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

        /// <summary>
        /// Decodes Base10 string to byte array.
        /// </summary>
        /// <param name="base10String">Base10 encoded string (length must be multiple of 3).</param>
        /// <returns>Decoded byte array.</returns>
        /// <exception cref="ArgumentException">Thrown for invalid input length.</exception>
        /// <exception cref="FormatException">Thrown for invalid characters or values.</exception>
        public static byte[] DecodeToBytes(string base10String)
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

        /// <summary>
        /// Gets encoding for specified encoding type.
        /// </summary>
        /// <param name="encoding">Encoding type to resolve.</param>
        /// <returns>Configured Encoding instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for unsupported encoding types.</exception>
        private static Encoding GetEncoding(StringEncoding encoding)
        {
            switch (encoding)
            {
                case StringEncoding.UTF8:
                    return Encoding.UTF8;
                case StringEncoding.UTF16LE:
                    return new UnicodeEncoding(false, false);
                case StringEncoding.UTF16BE:
                    return new UnicodeEncoding(true, false);
                case StringEncoding.ASCII:
                    return Encoding.ASCII;
                case StringEncoding.UTF32:
                    return new UTF32Encoding(false, false);
#pragma warning disable CS0618, SYSLIB0001
                case StringEncoding.UTF7:
                    return Encoding.UTF7;
#pragma warning restore CS0618, SYSLIB0001
#if NET6_0_OR_GREATER
                case StringEncoding.Latin1:
                    return Encoding.Latin1;
#endif
                default:
                    throw new ArgumentOutOfRangeException(nameof(encoding));
            }
        }
    }

    /// <summary>
    /// Provides extension methods for Base10 encoding and decoding.
    /// </summary>
    public static class Base10Extension
    {
        /// <summary>
        /// Encodes string to Base10 format.
        /// </summary>
        /// <param name="input">String to encode.</param>
        /// <param name="encoding">Character encoding to use (default: UTF8).</param>
        /// <returns>Base10 encoded string.</returns>
        public static string EncodeBase10(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base10.Encode(input, encoding);

        /// <summary>
        /// Decodes Base10 string to original string.
        /// </summary>
        /// <param name="input">Base10 encoded string.</param>
        /// <param name="encoding">Character encoding to use (default: UTF8).</param>
        /// <returns>Decoded original string.</returns>
        public static string DecodeBase10(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base10.Decode(input, encoding);

        /// <summary>
        /// Encodes byte array to Base10 format.
        /// </summary>
        /// <param name="bytes">Byte array to encode.</param>
        /// <returns>Base10 encoded string.</returns>
        public static string EncodeBase10(this byte[] bytes) => Base10.EncodeBytes(bytes);

        /// <summary>
        /// Decodes Base10 string to byte array.
        /// </summary>
        /// <param name="base10">Base10 encoded string.</param>
        /// <returns>Decoded byte array.</returns>
        public static byte[] DecodeBase10(this string base10) => Base10.DecodeToBytes(base10);
    }
}