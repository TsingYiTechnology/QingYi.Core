using System;
using System.Text;

namespace QingYi.Core.String.Base
{
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

        public static string EncodeString(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            var bytes = GetEncoding(encoding).GetBytes(input);
            return Encode(bytes);
        }

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
#pragma warning disable 0618
                StringEncoding.UTF7 => Encoding.UTF7,
#pragma warning restore 0618
                _ => throw new ArgumentOutOfRangeException(nameof(encoding))
            };
        }

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
}
