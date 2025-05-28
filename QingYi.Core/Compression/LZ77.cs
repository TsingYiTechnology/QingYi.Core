using System;
using System.Collections.Generic;

namespace QingYi.Core.Compression
{
    /// <summary>
    /// Represents a single token in LZ77 compressed data
    /// </summary>
    public struct Lz77Token
    {
        /// <summary>
        /// Offset to the start of matching data in the search buffer. 
        /// Value 0 indicates no match found.
        /// </summary>
        public int Offset;

        /// <summary>
        /// Length of the matching data sequence
        /// </summary>
        public int Length;

        /// <summary>
        /// Next literal byte after the matched sequence
        /// </summary>
        public byte NextByte;

        /// <summary>
        /// Returns a formatted string representation of the token
        /// </summary>
        /// <returns>
        /// String in format (Offset, Length, 'Char') for printable characters 
        /// or (Offset, Length, 0xXX) for non-printable bytes
        /// </returns>
        public override string ToString()
        {
            if (NextByte >= 32 && NextByte <= 126)
            {
                return $"({Offset}, {Length}, '{(char)NextByte}')";
            }
            return $"({Offset}, {Length}, 0x{NextByte:X2})";
        }
    }

    /// <summary>
    /// Provides LZ77 compression and decompression functionality
    /// </summary>
    public class LZ77
    {
        /// <summary>
        /// Compresses input data using LZ77 algorithm
        /// </summary>
        /// <param name="data">Input byte array to compress</param>
        /// <param name="searchBufferSize">
        /// Maximum size of the search buffer (sliding window). 
        /// Default is 1024 bytes.
        /// </param>
        /// <param name="lookAheadBufferSize">
        /// Maximum size of the look-ahead buffer. 
        /// Default is 256 bytes.
        /// </param>
        /// <returns>
        /// Array of LZ77 tokens representing the compressed data
        /// </returns>
        /// <remarks>
        /// Output tokens will always contain at least one byte (literal or match+literal).
        /// The last token may have NextByte=0 as placeholder when at end of data.
        /// </remarks>
        public static Lz77Token[] Encode(byte[] data, int searchBufferSize = 1024, int lookAheadBufferSize = 256)
        {
            List<Lz77Token> compressed = new List<Lz77Token>();
            int position = 0;

            while (position < data.Length)
            {
                int maxMatchLength = Math.Min(lookAheadBufferSize, data.Length - position);
                int searchStart = Math.Max(0, position - searchBufferSize);
                int bestOffset = 0;
                int bestLength = 0;

                // 在搜索缓冲区中寻找最长匹配
                for (int start = searchStart; start < position; start++)
                {
                    int length = 0;
                    while (length < maxMatchLength &&
                           data[start + length] == data[position + length])
                    {
                        length++;
                    }

                    if (length > bestLength)
                    {
                        bestOffset = position - start;
                        bestLength = length;
                    }
                }

                // 处理匹配结果
                if (bestLength > 0)
                {
                    // 检查是否到达数据末尾
                    byte nextByte = (position + bestLength < data.Length)
                        ? data[position + bestLength]
                        : (byte)0;

                    compressed.Add(new Lz77Token
                    {
                        Offset = bestOffset,
                        Length = bestLength,
                        NextByte = nextByte
                    });
                    position += bestLength + 1;
                }
                else
                {
                    // 无匹配情况
                    compressed.Add(new Lz77Token
                    {
                        Offset = 0,
                        Length = 0,
                        NextByte = data[position]
                    });
                    position++;
                }
            }

            return compressed.ToArray();
        }

        /// <summary>
        /// Decompresses LZ77 tokenized data (tuple version)
        /// </summary>
        /// <param name="compressed">
        /// Compressed data as list of (offset, length, nextByte) tuples
        /// </param>
        /// <returns>Decompressed byte array</returns>
        /// <remarks>
        /// <para>Structure of each token:</para>
        /// <list type="bullet">
        /// <item>When length=0: Outputs single literal byte (nextByte)</item>
        /// <item>When length>0: Copies [length] bytes from [offset] positions back, 
        /// then outputs nextByte (unless at end of stream)</item>
        /// </list>
        /// </remarks>
        public static byte[] Decode(List<(int offset, int length, byte nextByte)> compressed)
        {
            List<byte> output = new List<byte>();

            foreach (var (offset, length, nextByte) in compressed)
            {
                if (length == 0)
                {
                    // 字面量标记
                    output.Add(nextByte);
                }
                else
                {
                    // 复制匹配数据
                    int startIndex = output.Count - offset;
                    for (int i = 0; i < length; i++)
                    {
                        output.Add(output[startIndex + i]);
                    }

                    // 添加下一个字符（非0占位符时）
                    if (nextByte != 0 || length == 0)
                    {
                        output.Add(nextByte);
                    }
                }
            }

            return output.ToArray();
        }

        /// <summary>
        /// Decompresses LZ77 tokenized data (struct version)
        /// </summary>
        /// <param name="tokens">Array of Lz77Token structures</param>
        /// <returns>Decompressed byte array</returns>
        /// <remarks>
        /// <para>Behavior per token:</para>
        /// <list type="bullet">
        /// <item>Zero-length tokens: Output NextByte as literal</item>
        /// <item>Non-zero length: Copy [Length] bytes from [Offset] positions back,
        /// then append NextByte (except for end-of-stream placeholder)</item>
        /// </list>
        /// </remarks>
        public static byte[] Decode(Lz77Token[] tokens)
        {
            List<byte> output = new List<byte>();

            foreach (var token in tokens)
            {
                if (token.Length == 0)
                {
                    // 字面量标记
                    output.Add(token.NextByte);
                }
                else
                {
                    // 复制匹配数据
                    int startIndex = output.Count - token.Offset;
                    for (int i = 0; i < token.Length; i++)
                    {
                        output.Add(output[startIndex + i]);
                    }

                    // 添加下一个字符（非0占位符时）
                    if (token.NextByte != 0 || token.Length == 0)
                    {
                        output.Add(token.NextByte);
                    }
                }
            }

            return output.ToArray();
        }
    }
}
