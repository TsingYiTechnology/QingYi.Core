#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QingYi.Core.Compression.Unsafe
{
    public struct Lz77Token
    {
        public int Offset;
        public int Length;
        public byte NextByte;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() =>
            NextByte is >= 32 and <= 126
                ? $"({Offset}, {Length}, '{(char)NextByte}')"
                : $"({Offset}, {Length}, 0x{NextByte:X2})";
    }

    public unsafe static class LZ77
    {
        private const int MinMatchLength = 4;
        private const int MaxVectorSize = 32;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Lz77Token[] Encode(byte[] data, int searchBufferSize = 1024, int lookAheadBufferSize = 256)
        {
            if (Avx2.IsSupported)
                return EncodeAvx2(data, searchBufferSize, lookAheadBufferSize);

            return EncodeFallback(data, searchBufferSize, lookAheadBufferSize);
        }

        private static unsafe Lz77Token[] EncodeAvx2(
            byte[] data, int searchBufferSize, int lookAheadBufferSize)
        {
            var compressed = new List<Lz77Token>();
            fixed (byte* dataPtr = data)
            {
                byte* current = dataPtr;
                byte* end = dataPtr + data.Length;

                while (current < end)
                {
                    byte* windowStart = (byte*)Math.Max((byte)dataPtr, (byte)(current - searchBufferSize));
                    byte* lookAheadEnd = (byte*)Math.Min((byte)(current + lookAheadBufferSize), (byte)end);
                    int bestOffset = 0;
                    int bestLength = 0;

                    // AVX2加速搜索
                    for (byte* candidate = windowStart; candidate < current; candidate++)
                    {
                        int matchLen = SimdMatchLength(candidate, current, (int)(lookAheadEnd - current));
                        if (matchLen > bestLength)
                        {
                            bestLength = matchLen;
                            bestOffset = (int)(current - candidate);
                        }
                    }

                    ProcessMatch(compressed, ref current, end, bestOffset, bestLength);
                }
            }
            return compressed.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int SimdMatchLength(byte* src, byte* dest, int maxLen)
        {
            int len = 0;
            int vectorBlocks = maxLen / MaxVectorSize;

            // AVX2批量比较
            while (vectorBlocks-- > 0)
            {
                Vector256<byte> v1 = Avx.LoadVector256(src + len);
                Vector256<byte> v2 = Avx.LoadVector256(dest + len);
                uint mask = (uint)Avx2.MoveMask(Avx2.CompareEqual(v1, v2));

                if (mask != 0xFFFFFFFF)
                {
                    len += BitScanForward(~mask);
                    return len;
                }
                len += MaxVectorSize;
            }

            // 处理剩余字节
            while (len < maxLen && src[len] == dest[len])
                len++;

            return len;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ProcessMatch(
            List<Lz77Token> compressed, ref byte* current, byte* end, int bestOffset, int bestLength)
        {
            if (bestLength >= MinMatchLength)
            {
                compressed.Add(new Lz77Token
                {
                    Offset = bestOffset,
                    Length = bestLength,
                    NextByte = (current + bestLength < end) ? current[bestLength] : (byte)0
                });
                current += bestLength + 1;
            }
            else
            {
                compressed.Add(new Lz77Token
                {
                    Offset = 0,
                    Length = 0,
                    NextByte = *current
                });
                current++;
            }
        }

        private static unsafe Lz77Token[] EncodeFallback(
            byte[] data, int searchBufferSize, int lookAheadBufferSize)
        {
            // 回退方案使用内存指针优化
            var compressed = new List<Lz77Token>();
            fixed (byte* dataPtr = data)
            {
                byte* current = dataPtr;
                byte* end = dataPtr + data.Length;

                while (current < end)
                {
                    byte* windowStart = (byte*)Math.Max((byte)dataPtr, (byte)(current - searchBufferSize));
                    byte* lookAheadEnd = (byte*)Math.Min((byte)(current + lookAheadBufferSize), (byte)end);
                    int bestOffset = 0;
                    int bestLength = 0;

                    for (byte* candidate = windowStart; candidate < current; candidate++)
                    {
                        int matchLen = 0;
                        byte* a = candidate;
                        byte* b = current;

                        while (b < lookAheadEnd && *a++ == *b++)
                            matchLen++;

                        if (matchLen > bestLength)
                        {
                            bestLength = matchLen;
                            bestOffset = (int)(current - candidate);
                            if (bestLength >= lookAheadBufferSize) break;
                        }
                    }

                    ProcessMatch(compressed, ref current, end, bestOffset, bestLength);
                }
            }
            return compressed.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Decode(Lz77Token[] tokens)
        {
            int outputLength = CalculateOutputLength(tokens);
            byte[] output = new byte[outputLength];

            fixed (Lz77Token* tokenPtr = tokens)
            fixed (byte* outputPtr = output)
            {
                Lz77Token* currentToken = tokenPtr;
                Lz77Token* endToken = tokenPtr + tokens.Length;
                byte* dest = outputPtr;

                while (currentToken < endToken)
                {
                    if (currentToken->Length == 0)
                    {
                        *dest++ = currentToken->NextByte;
                    }
                    else
                    {
                        byte* src = dest - currentToken->Offset;
                        MemCopyInline(dest, src, currentToken->Length);
                        dest += currentToken->Length;

                        if (currentToken->NextByte != 0)
                            *dest++ = currentToken->NextByte;
                    }
                    currentToken++;
                }
            }
            return output;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculateOutputLength(Lz77Token[] tokens)
        {
            int length = 0;
            foreach (var token in tokens)
                length += token.Length + (token.NextByte != 0 || token.Length == 0 ? 1 : 0);
            return length;
        }

        // 使用IL内联汇编实现内存复制
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MemCopyInline(byte* dest, byte* src, int count)
        {
            if (count <= 0) return;

            // 小内存块使用寄存器复制
            if (count <= 16)
            {
                while (count >= 8)
                {
                    *(ulong*)dest = *(ulong*)src;
                    dest += 8;
                    src += 8;
                    count -= 8;
                }
                while (count-- > 0) *dest++ = *src++;
                return;
            }

            // 大内存块使用AVX加速
            if (Avx2.IsSupported && count >= 32)
            {
                int vectorBlocks = count / MaxVectorSize;
                for (int i = 0; i < vectorBlocks; i++)
                {
                    Avx.Store(dest, Avx.LoadVector256(src));
                    dest += MaxVectorSize;
                    src += MaxVectorSize;
                }
                count %= MaxVectorSize;
            }

            // 处理剩余字节
            while (count >= 8)
            {
                *(ulong*)dest = *(ulong*)src;
                dest += 8;
                src += 8;
                count -= 8;
            }
            while (count-- > 0) *dest++ = *src++;
        }

        // 位扫描优化
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BitScanForward(uint value)
        {
            if (value == 0) return 32;
            int pos = 0;
            while ((value & 1) == 0)
            {
                value >>= 1;
                pos++;
            }
            return pos;
        }
    }
}
#endif