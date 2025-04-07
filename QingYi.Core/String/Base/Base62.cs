using System;
using System.Collections.Generic;
using System.Text;

namespace QingYi.Core.String.Base
{
    public class Base62
    {
        private const string CharSet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private static readonly Dictionary<char, int> CharMap = CreateCharMap();
        private static readonly Encoding[] Encodings = CreateEncodingsTable();

        private static Dictionary<char, int> CreateCharMap()
        {
            var map = new Dictionary<char, int>(62);
            for (int i = 0; i < CharSet.Length; i++)
                map[CharSet[i]] = i;
            return map;
        }

        /// <summary>
        /// Gets the base62-encoded character set.<br />
        /// 获取 Base62 编码的字符集。
        /// </summary>
        /// <returns>The base62-encoded character set.<br />Base62 编码的字符集</returns>
        public override string ToString() => CharSet;

        private static Encoding[] CreateEncodingsTable()
        {
            var encodings = new Encoding[Enum.GetValues(typeof(StringEncoding)).Length];
            encodings[(int)StringEncoding.UTF8] = Encoding.UTF8;
            encodings[(int)StringEncoding.UTF16LE] = new UnicodeEncoding(false, false);
            encodings[(int)StringEncoding.UTF16BE] = new UnicodeEncoding(true, false);
            encodings[(int)StringEncoding.ASCII] = Encoding.ASCII;
            encodings[(int)StringEncoding.UTF32] = Encoding.UTF32;
#if NET6_0_OR_GREATER
        encodings[(int)StringEncoding.Latin1] = Encoding.Latin1;
#endif
#pragma warning disable 0618, SYSLIB0001
            encodings[(int)StringEncoding.UTF7] = Encoding.UTF7;
#pragma warning restore 0618, SYSLIB0001
            return encodings;
        }

        public static unsafe string Encode(string input, StringEncoding encoding)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length == 0) return string.Empty;

            Encoding enc = Encodings[(int)encoding] ?? throw new NotSupportedException();
            int byteCount = enc.GetByteCount(input);
            byte[] buffer = new byte[byteCount];

            fixed (char* pInput = input)
            fixed (byte* pBuffer = buffer)
            {
                enc.GetBytes(pInput, input.Length, pBuffer, byteCount);
            }

            return Encode(buffer);
        }

        public static unsafe string Encode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) return string.Empty;

            int outputLength = (int)Math.Ceiling(data.Length * 8 / Math.Log(62, 2));
            char[] output = new char[outputLength];
            int outputIndex = outputLength;

            uint carry = 0;
            int bits = 0;

            fixed (byte* pData = data)
            fixed (char* pOutput = output)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    carry = (carry << 8) | pData[i];
                    bits += 8;

                    while (bits >= 6)
                    {
                        bits -= 6;
                        uint temp = carry >> bits;
                        pOutput[--outputIndex] = CharSet[(int)(temp % 62)];
                        carry &= (1U << bits) - 1;
                    }
                }

                if (bits > 0)
                {
                    pOutput[--outputIndex] = CharSet[(int)((carry << (6 - bits)) % 62)];
                }
            }

            return new string(output, outputIndex, outputLength - outputIndex);
        }

        public static unsafe string Decode(string base62, StringEncoding encoding)
        {
            byte[] bytes = DecodeToBytes(base62);
            Encoding enc = Encodings[(int)encoding] ?? throw new NotSupportedException();

            fixed (byte* pBytes = bytes)
            {
                return enc.GetString(pBytes, bytes.Length);
            }
        }

        public static unsafe byte[] DecodeToBytes(string base62)
        {
            if (base62 == null) throw new ArgumentNullException(nameof(base62));
            if (base62.Length == 0) return Array.Empty<byte>();

            int outputLength = (int)Math.Ceiling(base62.Length * Math.Log(62, 2) / 8);
            byte[] output = new byte[outputLength];
            int outputIndex = outputLength;

            uint carry = 0;
            int bits = 0;

            fixed (char* pInput = base62)
            fixed (byte* pOutput = output)
            {
                for (int i = 0; i < base62.Length; i++)
                {
                    if (!CharMap.TryGetValue(pInput[i], out int value))
                        throw new ArgumentException($"Invalid Base62 character: {pInput[i]}");

                    carry = carry * 62 + (uint)value;
                    bits += 6;

                    while (bits >= 8)
                    {
                        bits -= 8;
                        pOutput[--outputIndex] = (byte)(carry >> bits);
                        carry &= (1U << bits) - 1;
                    }
                }

                if (bits > 0)
                {
                    pOutput[--outputIndex] = (byte)(carry << (8 - bits));
                }
            }

            if (outputIndex == 0) return output;
            byte[] result = new byte[outputLength - outputIndex];
            Buffer.BlockCopy(output, outputIndex, result, 0, result.Length);
            return result;
        }
    }
}
