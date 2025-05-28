using System;
using System.Collections.Generic;

namespace QingYi.Core.Compression
{
    public struct Lz77Token
    {
        public int Offset;    // 匹配偏移量（0表示无匹配）
        public int Length;    // 匹配长度
        public byte NextByte; // 下一个字符（或字面量）

        /// <inheritdoc/>
        public override string ToString()
        {
            if (NextByte >= 32 && NextByte <= 126)
            {
                return $"({Offset}, {Length}, '{(char)NextByte}')";
            }
            return $"({Offset}, {Length}, 0x{NextByte:X2})";
        }
    }

    public class LZ77
    {
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
