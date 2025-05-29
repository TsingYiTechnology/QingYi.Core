using System;
using System.Runtime.CompilerServices;
using System.Text;

#pragma warning disable SYSLIB0001, CS0618

namespace QingYi.Core.Codec.Base
{
    /// <summary>
    /// Base32 codec library (Crockford's Base32).<br />
    /// Base32 编解码库（Crockford's Base32）。
    /// </summary>
    public class Base32Crockford
    {
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private static readonly byte[] CharMap = new byte[256];

        static Base32Crockford()
        {
            for (int i = 0; i < 256; i++) CharMap[i] = 0xFF;
            for (byte i = 0; i < Alphabet.Length; i++)
            {
                char c = Alphabet[i];
                CharMap[c] = i;
                CharMap[char.ToLowerInvariant(c)] = i;
            }

            CharMap['O'] = 0; CharMap['o'] = 0;
            CharMap['I'] = 1; CharMap['i'] = 1;
            CharMap['L'] = 1; CharMap['l'] = 1;
        }

        /// <summary>
        /// Gets the base32-encoded character set.<br />
        /// 获取 Base32 编码的字符集。
        /// </summary>
        /// <returns>The base32-encoded character set.<br />Base32 编码的字符集</returns>
        public override string ToString() => Alphabet;

        /// <summary>
        /// Base36 encoding of the string.<br />
        /// 将字符串进行Base32编码。
        /// </summary>
        /// <param name="source">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encodingType">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string Encode(string source, StringEncoding encodingType = StringEncoding.UTF8)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Length == 0) return string.Empty;

            byte[] bytes = GetEncoding(encodingType).GetBytes(source);
            return EncodeBytes(bytes);
        }

        /// <summary>
        /// Base36 decoding of the string.<br />
        /// 将字符串进行Base32解码。
        /// </summary>
        /// <param name="encoded">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encodingType">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string Decode(string encoded, StringEncoding encodingType = StringEncoding.UTF8)
        {
            if (encoded == null) throw new ArgumentNullException(nameof(encoded));
            if (encoded.Length == 0) return string.Empty;

            byte[] bytes = DecodeBytes(encoded);
            return GetEncoding(encodingType).GetString(bytes);
        }

        private static unsafe string EncodeBytes(byte[] input)
        {
            int inputLength = input.Length;
            int outputLength = (inputLength * 8 + 4) / 5;
            char[] output = new char[outputLength];
            int outputIndex = 0;

            ulong buffer = 0;
            int bufferBits = 0;

            fixed (byte* pInput = input)
            fixed (char* pOutput = output)
            {
                byte* pIn = pInput;
                byte* pEnd = pInput + inputLength;
                char* pOut = pOutput;

                while (pIn < pEnd)
                {
                    buffer = buffer << 8 | *pIn++;
                    bufferBits += 8;

                    while (bufferBits >= 5)
                    {
                        bufferBits -= 5;
                        byte value = (byte)(buffer >> bufferBits & 0x1F);
                        *pOut++ = Alphabet[value];
                        outputIndex++;
                    }
                }

                if (bufferBits > 0)
                {
                    byte value = (byte)(buffer << 5 - bufferBits & 0x1F);
                    *pOut++ = Alphabet[value];
                    outputIndex++;
                }
            }

            return new string(output, 0, outputIndex);
        }

        private static unsafe byte[] DecodeBytes(string encoded)
        {
            int validCharCount = 0;
            fixed (char* pEncoded = encoded)
            {
                char* p = pEncoded;
                char* pEnd = p + encoded.Length;
                while (p < pEnd)
                {
                    char c = *p++;
                    if (IsIgnoredChar(c)) continue;
                    if (CharMap[c] == 0xFF) throw new ArgumentException("Invalid character: " + c);
                    validCharCount++;
                }
            }

            if (validCharCount == 0) return Array.Empty<byte>();
            int outputLength = validCharCount * 5 / 8;
            byte[] output = new byte[outputLength];
            int outputIndex = 0;

            ulong buffer = 0;
            int bufferBits = 0;

            fixed (char* pEncoded = encoded)
            fixed (byte* pOutput = output)
            {
                char* p = pEncoded;
                char* pEnd = p + encoded.Length;
                byte* pOut = pOutput;

                while (p < pEnd)
                {
                    char c = *p++;
                    if (IsIgnoredChar(c)) continue;

                    buffer = buffer << 5 | CharMap[c];
                    bufferBits += 5;

                    while (bufferBits >= 8)
                    {
                        bufferBits -= 8;
                        *pOut++ = (byte)(buffer >> bufferBits & 0xFF);
                        outputIndex++;
                    }
                }
            }

            return output;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsIgnoredChar(char c) => c == '-' || char.IsWhiteSpace(c);

        private static Encoding GetEncoding(StringEncoding encodingType)
        {
            switch (encodingType)
            {
                case StringEncoding.UTF8:
                    return Encoding.UTF8;
                case StringEncoding.UTF16LE:
                    return Encoding.Unicode;
                case StringEncoding.UTF16BE:
                    return Encoding.BigEndianUnicode;
                case StringEncoding.UTF32:
                    return Encoding.UTF32;
#if NET6_0_OR_GREATER
                case StringEncoding.Latin1:
                    return Encoding.GetEncoding(28591);
#endif
                case StringEncoding.ASCII:
                    return Encoding.ASCII;
                case StringEncoding.UTF7:
                    return Encoding.UTF7;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
