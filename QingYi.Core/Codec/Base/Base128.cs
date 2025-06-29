#if NET5_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Text;

namespace QingYi.Core.Codec.Base
{
    using System;
    using System.Text;

    public static class Base128
    {
        public static byte[] Encode(byte[] input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (input.Length == 0)
                return new byte[1] { 0 }; // 只有填充位信息

            int inputBitCount = input.Length * 8;
            int numChunks = (inputBitCount + 6) / 7; // 向上取整
            byte[] output = new byte[numChunks + 1]; // 最后一位存储填充位数

            ulong bitBuffer = 0;
            int bufferLength = 0;
            int paddingBits = 0;

            unsafe
            {
                fixed (byte* pInput = input)
                fixed (byte* pOutput = output)
                {
                    byte* pIn = pInput;
                    byte* pInEnd = pIn + input.Length;
                    byte* pOut = pOutput;

                    for (int i = 0; i < numChunks; i++)
                    {
                        if (bufferLength < 7)
                        {
                            if (pIn < pInEnd)
                            {
                                bitBuffer = (bitBuffer << 8) | *pIn++;
                                bufferLength += 8;
                            }
                            else
                            {
                                // 输入结束，填充零位
                                paddingBits = 7 - bufferLength;
                                bitBuffer <<= paddingBits;
                                bufferLength += paddingBits;
                            }
                        }

                        // 提取7位
                        int shift = bufferLength - 7;
                        byte chunk = (byte)((bitBuffer >> shift) & 0x7F);
                        *pOut++ = chunk;

                        // 更新缓冲区
                        bitBuffer &= (1UL << shift) - 1;
                        bufferLength -= 7;
                    }

                    // 存储填充位数
                    output[output.Length - 1] = (byte)paddingBits;
                }
            }

            return output;
        }

        public static byte[] Decode(byte[] encoded)
        {
            if (encoded == null || encoded.Length < 1)
                throw new ArgumentException("Invalid encoded data.");

            int paddingBits = encoded[encoded.Length - 1];
            if (paddingBits < 0 || paddingBits > 6)
                throw new ArgumentException("Invalid padding bits.");

            int numChunks = encoded.Length - 1;

            // 处理空输入情况
            if (numChunks == 0)
                return Array.Empty<byte>();

            int totalBits = numChunks * 7 - paddingBits;
            int outputLength = totalBits / 8;
            byte[] output = new byte[outputLength];

            ulong bitBuffer = 0;
            int bufferLength = 0;

            unsafe
            {
                fixed (byte* pEncoded = encoded)
                fixed (byte* pOutput = output)
                {
                    byte* pIn = pEncoded;
                    byte* pInEnd = pIn + numChunks;
                    byte* pOut = pOutput;
                    byte* pOutEnd = pOut + outputLength;

                    while (pIn < pInEnd)
                    {
                        // 添加显式转换为ulong
                        bitBuffer = (bitBuffer << 7) | (ulong)(*pIn++ & 0x7F);
                        bufferLength += 7;

                        // 提取完整字节
                        while (bufferLength >= 8 && pOut < pOutEnd)
                        {
                            int shift = bufferLength - 8;
                            byte value = (byte)((bitBuffer >> shift) & 0xFF);
                            *pOut++ = value;
                            bitBuffer &= (1UL << shift) - 1;
                            bufferLength -= 8;
                        }
                    }
                }
            }

            return output;
        }

        public static byte[] EncodeString(string input, StringEncoding encoding)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            Encoding encoder = GetEncoding(encoding);
            byte[] bytes = encoder.GetBytes(input);
            return Encode(bytes);
        }

        public static string DecodeToString(byte[] encoded, StringEncoding encoding)
        {
            byte[] decodedBytes = Decode(encoded);
            Encoding decoder = GetEncoding(encoding);
            return decoder.GetString(decodedBytes);
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
#pragma warning disable SYSLIB0001, CS0618
                StringEncoding.UTF7 => Encoding.UTF7,
#pragma warning restore SYSLIB0001, CS0618
                _ => throw new ArgumentException("Unsupported encoding.", nameof(encoding))
            };
        }
    }
}
#endif
