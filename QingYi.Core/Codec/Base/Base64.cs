using System;
using System.Runtime.CompilerServices;
using System.Text;

#pragma warning disable CS0675, SYSLIB0001

namespace QingYi.Core.Codec.Base
{
#if NET6_0_OR_GREATER

    /// <summary>
    /// 适用于 .NET 6.0 及更高版本的 Base 64 编解码库。<br />
    /// Base 64 codec library for.NET 6.0 and higher.
    /// </summary>
    public unsafe class Base64
    {
        private const string Base64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        private static readonly sbyte[] Base64Inv = new sbyte[128];

        private static readonly Encoding[] Encoders =
        {
            Encoding.UTF8,                          // UTF8
            Encoding.Unicode,                       // UTF16LE
            Encoding.BigEndianUnicode,              // UTF16BE
            Encoding.ASCII,
            Encoding.Latin1,
            Encoding.UTF32,
            Encoding.UTF7,
        };

        static Base64()
        {
            // 注册代码页编码提供程序
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // 初始化编码器数组
            Encoders = new Encoding[]
            {
                Encoding.UTF8,                          // UTF8
                Encoding.Unicode,                       // UTF16LE
                Encoding.BigEndianUnicode,              // UTF16BE
                Encoding.ASCII,
                Encoding.Latin1,
                Encoding.UTF32,
                Encoding.UTF7,
            };

            // 初始化 Base64 反向查找表
            for (int i = 0; i < Base64Inv.Length; i++) Base64Inv[i] = -1;
            for (int i = 0; i < Base64Chars.Length; i++) Base64Inv[Base64Chars[i]] = (sbyte)i;
            Base64Inv['='] = 0;
        }

        /// <summary>
        /// Gets the base64-encoded character set.<br />
        /// 获取 Base64 编码的字符集。
        /// </summary>
        /// <returns>The base64-encoded character set.<br />Base64 编码的字符集</returns>
        public override string ToString() => Base64Chars;

        /// <summary>
        /// 将字符串进行 Base 64 编码。<br />
        /// Base 64 encoding of the string.
        /// </summary>
        /// <param name="input">输入文本<br />Input text</param>
        /// <param name="encoding">字符编码标准<br />Character coding standard</param>
        /// <returns>被编码的字符串<br />Encoded string</returns>
        public static string Encode(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var bytes = GetBytes(input, Encoders[(int)encoding]);
            return BytesToBase64(bytes);
        }

        /// <summary>
        /// 将字符串进行 Base 64 编码。<br />
        /// Base 64 decoding of the string.
        /// </summary>
        /// <param name="base64">输入文本<br />Input text</param>
        /// <param name="encoding">字符编码标准<br />Character coding standard</param>
        /// <returns>被解码的字符串<br />Decoded string</returns>
        public static string Decode(string base64, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (string.IsNullOrEmpty(base64)) return string.Empty;

            var bytes = Base64ToBytes(base64);
            return GetString(bytes, Encoders[(int)encoding]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] GetBytes(string s, Encoding encoding)
        {
            if (encoding == Encoding.Unicode || encoding == Encoding.BigEndianUnicode)
            {
                int byteCount = encoding.GetByteCount(s);
                byte[] buffer = new byte[byteCount];
                fixed (char* pChar = s)
                fixed (byte* pBuffer = buffer)
                {
                    encoding.GetBytes(pChar, s.Length, pBuffer, byteCount);
                }
                return buffer;
            }
            return encoding.GetBytes(s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetString(byte[] bytes, Encoding encoding)
        {
            if (bytes.Length == 0) return string.Empty;

            fixed (byte* pBytes = bytes)
            {
                return encoding.GetString(pBytes, bytes.Length);
            }
        }

        /// <summary>
        /// 将字节数组进行 Base 64 编码。<br />
        /// Base 64 encoding of the bytes.
        /// </summary>
        /// <param name="input">输入字节数组<br />Input bytes</param>
        /// <returns>被编码的字符串<br />Encoded string</returns>
        public static string BytesToBase64(byte[] input)
        {
            int inputLength = input.Length;
            int outputLength = (inputLength + 2) / 3 * 4;
            char[] output = new char[outputLength];

            fixed (byte* inputPtr = input)
            fixed (char* outputPtr = output)
            fixed (char* base64Ptr = Base64Chars)
            {
                byte* src = inputPtr;
                char* dest = outputPtr;

                int wholeBlocks = inputLength / 3;
                int remainingBytes = inputLength % 3;

                for (int i = 0; i < wholeBlocks; i++)
                {
                    uint triple = (uint)*src++ << 16;
                    triple |= (uint)*src++ << 8;
                    triple |= *src++;

                    *dest++ = base64Ptr[triple >> 18 & 0x3F];
                    *dest++ = base64Ptr[triple >> 12 & 0x3F];
                    *dest++ = base64Ptr[triple >> 6 & 0x3F];
                    *dest++ = base64Ptr[triple & 0x3F];
                }

                if (remainingBytes > 0)
                {
                    uint triple = (uint)*src++ << 16;
                    if (remainingBytes > 1) triple |= (uint)*src++ << 8;

                    *dest++ = base64Ptr[triple >> 18 & 0x3F];
                    *dest++ = base64Ptr[triple >> 12 & 0x3F];

                    if (remainingBytes > 1)
                    {
                        *dest++ = base64Ptr[triple >> 6 & 0x3F];
                    }
                    else
                    {
                        *dest++ = '=';
                    }

                    *dest = '=';
                }
            }

            return new string(output);
        }

        /// <summary>
        /// 将字符串进行 Base 64 解码。<br />
        /// Base 64 decoding of the string.
        /// </summary>
        /// <param name="base64">输入文本<br />Input text</param>
        /// <returns>被解码的字节数组<br />Decoded bytes</returns>
        public static byte[] Base64ToBytes(string base64)
        {
            int inputLength = base64.Length;
            if (inputLength == 0) return Array.Empty<byte>();

            int outputLength = inputLength * 3 / 4;
            if (base64[inputLength - 1] == '=') outputLength--;
            if (inputLength >= 2 && base64[inputLength - 2] == '=') outputLength--;

            byte[] output = new byte[outputLength];

            fixed (char* inputPtr = base64)
            fixed (byte* outputPtr = output)
            {
                char* src = inputPtr;
                byte* dest = outputPtr;

                int wholeBlocks = inputLength / 4; // 修复此处
                int remainingBytes = inputLength % 4;

                for (int i = 0; i < wholeBlocks; i++)
                {
                    uint quad = (uint)Base64Inv[*src++] << 18;
                    quad |= (uint)Base64Inv[*src++] << 12;
                    quad |= (uint)Base64Inv[*src++] << 6;
                    quad |= (uint)Base64Inv[*src++];

                    *dest++ = (byte)(quad >> 16);
                    *dest++ = (byte)(quad >> 8);
                    *dest++ = (byte)quad;
                }

                if (remainingBytes > 0)
                {
                    uint quad = 0;
                    int shift = 18;
                    int bytesToWrite = remainingBytes - 1;

                    for (int i = 0; i < 4; i++)
                    {
                        if (i < remainingBytes)
                        {
                            quad |= (uint)Base64Inv[*src++] << shift;
                        }
                        shift -= 6;
                    }

                    *dest++ = (byte)(quad >> 16);
                    if (bytesToWrite >= 2) *dest++ = (byte)(quad >> 8);
                    if (bytesToWrite >= 3) *dest = (byte)quad;
                }
            }

            return output;
        }
    }
#else
    /// <summary>
    /// 适用于.NET Standard 2.1的 Base 64 编解码类。
    /// Base 64 codec class for.NET Standard 2.1.<br />
    /// </summary>
    public class Base64
    {
        private static readonly char[] s_base64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/".ToCharArray();
        private static readonly int[] s_decodeTable = new int[256];

        static Base64()
        {
            for (int i = 0; i < 256; i++) s_decodeTable[i] = -1;
            for (int i = 0; i < s_base64Chars.Length; i++) s_decodeTable[s_base64Chars[i]] = i;
            s_decodeTable['='] = 0; // 特殊处理填充字符
        }

        /// <summary>
        /// Gets the base64-encoded character set.<br />
        /// 获取 Base64 编码的字符集。
        /// </summary>
        /// <returns>The base64-encoded character set.<br />Base64 编码的字符集</returns>
        public override string ToString() => "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

        // Base64编码（字符串→Base64字符串）
        /// <summary>
        /// 将字符串进行 Base 64 编码。<br />
        /// Base 64 encoding of the string.
        /// </summary>
        /// <param name="text">输入文本<br />Input text</param>
        /// <returns>被编码的字符串<br />Encoded string</returns>
        public static string Encode(string text) => BytesToBase64(Encoding.UTF8.GetBytes(text));

        // Base64编码（字节数组→Base64字符串）
        /// <summary>
        /// 将字节数组进行 Base 64 编码。<br />
        /// Base 64 encoding of the bytes.
        /// </summary>
        /// <param name="bytes">输入字节数组<br />Input bytes</param>
        /// <returns>被编码的字符串<br />Encoded string</returns>
        public static unsafe string BytesToBase64(byte[] bytes)
        {
            int inputLength = bytes.Length;
            int outputLength = (inputLength + 2) / 3 * 4;
            char[] output = new char[outputLength];

            fixed (byte* inputPtr = bytes)
            fixed (char* outputPtr = output)
            {
                byte* inPtr = inputPtr;
                char* outPtr = outputPtr;
                int remaining = inputLength;

                // 处理完整3字节组
                while (remaining >= 3)
                {
                    uint value = (uint)*inPtr++ << 16;
                    value |= (uint)*inPtr++ << 8;
                    value |= *inPtr++;

                    *outPtr++ = s_base64Chars[value >> 18 & 0x3F];
                    *outPtr++ = s_base64Chars[value >> 12 & 0x3F];
                    *outPtr++ = s_base64Chars[value >> 6 & 0x3F];
                    *outPtr++ = s_base64Chars[value & 0x3F];
                    remaining -= 3;
                }

                // 处理剩余字节
                if (remaining > 0)
                {
                    uint value = (uint)*inPtr++ << 16;
                    if (remaining == 2) value |= (uint)*inPtr++ << 8;

                    *outPtr++ = s_base64Chars[value >> 18 & 0x3F];
                    *outPtr++ = s_base64Chars[value >> 12 & 0x3F];
                    *outPtr++ = remaining == 2 ? s_base64Chars[value >> 6 & 0x3F] : '=';
                    *outPtr++ = '=';
                }
            }

            return new string(output);
        }

        // Base64解码（Base64字符串→字符串）
        /// <summary>
        /// 将字符串进行 Base 64 编码。<br />
        /// Base 64 decoding of the string.
        /// </summary>
        /// <param name="base64Text">输入文本<br />Input text</param>
        /// <returns>被解码的字符串<br />Decoded string</returns>
        public static string Decode(string base64Text) => Encoding.UTF8.GetString(Base64ToBytes(base64Text));

        // Base64解码（Base64字符串→字节数组）
        /// <summary>
        /// 将字符串进行 Base 64 解码。<br />
        /// Base 64 decoding of the string.
        /// </summary>
        /// <param name="base64Text">输入文本<br />Input text</param>
        /// <returns>被解码的字节数组<br />Decoded bytes</returns>
        public static unsafe byte[] Base64ToBytes(string base64Text)
        {
            int inputLength = base64Text.Length;
            char[] cleaned = new char[inputLength];
            int cleanLength = 0;
            int padding = 0;
            bool hasPadding = false;

            // 预处理：过滤无效字符并验证格式
            fixed (char* inputPtr = base64Text)
            {
                char* ptr = inputPtr;
                for (int i = 0; i < inputLength; i++)
                {
                    char c = *ptr++;
                    if (c == '=')
                    {
                        if (hasPadding) padding++;
                        cleaned[cleanLength++] = c;
                        hasPadding = true;
                    }
                    else if (s_decodeTable[c] != -1)
                    {
                        if (hasPadding) throw new FormatException("Invalid padding position");
                        cleaned[cleanLength++] = c;
                    }
                }
            }

            // 验证长度和填充
            if (cleanLength % 4 != 0) throw new FormatException("Invalid base64 length");
            if (padding > 2) throw new FormatException("Too many padding characters");

            // 计算输出长度
            int outputLength = cleanLength * 3 / 4 - padding;
            byte[] output = new byte[outputLength];

            fixed (char* inputPtr = cleaned)
            fixed (byte* outputPtr = output)
            {
                char* inPtr = inputPtr;
                byte* outPtr = outputPtr;
                int remaining = cleanLength;

                // 处理完整4字符组
                while (remaining >= 4)
                {
                    int a = s_decodeTable[*inPtr++];
                    int b = s_decodeTable[*inPtr++];
                    int c = s_decodeTable[*inPtr++];
                    int d = s_decodeTable[*inPtr++];

                    uint value = (uint)a << 18 | (uint)b << 12 | (uint)c << 6 | (uint)d;
                    *outPtr++ = (byte)(value >> 16);

                    if (*inPtr != '=')  // 非填充组
                    {
                        *outPtr++ = (byte)(value >> 8);
                        *outPtr++ = (byte)value;
                    }
                    else if (remaining == 4 && c == 0)  // 1字节填充
                    {
                        if (d != 0) throw new FormatException("Invalid padding");
                    }
                    else if (remaining == 4)  // 2字节填充
                    {
                        *outPtr++ = (byte)(value >> 8);
                    }

                    remaining -= 4;
                }
            }

            return output;
        }
    }
#endif
    /// <summary>
    /// 适用于字符串简易编解码 Base 64 的静态类。<br />
    /// 使用代码如：<em>string text = "Hello World!".EncodeBase64()</em><br />
    /// <br />
    /// Static class for simple string codec Base 64.<br />
    /// Use code such as: <em>string text = "Hello World!".EncodeBase64()</em>
    /// </summary>
    public static class Base64Extension
    {
#if NET6_0_OR_GREATER
        /// <summary>
        /// 将字符串进行 Base 64 编码。<br />
        /// Base 64 encoding of the string.
        /// </summary>
        /// <param name="input">输入文本<br />Input text</param>
        /// <param name="encoding">字符编码标准<br />Character coding standard</param>
        /// <returns>被编码的字符串<br />Encoded string</returns>
        public static string EncodeBase64(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base64.Encode(input, encoding);

        /// <summary>
        /// 将字符串进行 Base 64 编码。<br />
        /// Base 64 decoding of the string.
        /// </summary>
        /// <param name="input">输入文本<br />Input text</param>
        /// <param name="encoding">字符编码标准<br />Character coding standard</param>
        /// <returns>被解码的字符串<br />Decoded string</returns>
        public static string DecodeBase64(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base64.Decode(input, encoding);
#else
        /// <summary>
        /// 将字符串进行 Base 64 编码。<br />
        /// Base 64 encoding of the string.
        /// </summary>
        /// <param name="input">输入文本<br />Input text</param>
        /// <returns>被编码的字符串<br />Encoded string</returns>
        public static string EncodeBase64(this string input) => Base64.Encode(input);

        /// <summary>
        /// 将字符串进行 Base 64 编码。<br />
        /// Base 64 decoding of the string.
        /// </summary>
        /// <param name="input">输入文本<br />Input text</param>
        /// <returns>被解码的字符串<br />Decoded string</returns>
        public static string DecodeBase64(this string input) => Base64.Decode(input);
#endif
        /// <summary>
        /// 将字节数组进行 Base 64 编码。<br />
        /// Base 64 encoding of the bytes.
        /// </summary>
        /// <param name="bytes">输入字节数组<br />Input bytes</param>
        /// <returns>被编码的字符串<br />Encoded string</returns>
        public static string EncodeBytesToBase64(this byte[] bytes) => Base64.BytesToBase64(bytes);

        /// <summary>
        /// 将字符串进行 Base 64 解码。<br />
        /// Base 64 decoding of the string.
        /// </summary>
        /// <param name="base64">输入文本<br />Input text</param>
        /// <returns>被解码的字节数组<br />Decoded bytes</returns>
        public static byte[] DecodeBase64ToBytes(this string base64) => Base64.Base64ToBytes(base64);
    }
}
