#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace QingYi.Core.Compression.Unsafe
{
    /// <summary>
    /// Represents a single token in LZ77 compressed data
    /// </summary>
    [DebuggerDisplay($"{{{nameof(DebuggerDisplay)}(),nq}}")]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly string ToString() =>
            NextByte is >= 32 and <= 126
                ? $"({Offset}, {Length}, '{(char)NextByte}')"
                : $"({Offset}, {Length}, 0x{NextByte:X2})";

        private readonly string DebuggerDisplay => ToString();
    }

    /// <summary>
    /// Provides unsafe-optimized LZ77 compression and decompression using AVX intrinsics
    /// </summary>
    public unsafe static class LZ77
    {
        private const int MinMatchLength = 4;
        private const int MaxVectorSize = 32;

        /// <summary>
        /// Compresses input data using LZ77 algorithm with AVX2 optimizations
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
        /// Uses AVX2 vector instructions when available for 32-byte parallel matching.
        /// Automatically falls back to pointer-optimized version when AVX2 unavailable.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Lz77Token[] Encode(byte[] data, int searchBufferSize = 1024, int lookAheadBufferSize = 256)
        {
            if (Avx2.IsSupported)
                return EncodeAvx2(data, searchBufferSize, lookAheadBufferSize);

            return EncodeFallback(data, searchBufferSize, lookAheadBufferSize);
        }

        /// <summary>
        /// AVX2-accelerated LZ77 compression implementation
        /// </summary>
        /// <param name="data">Input data to compress</param>
        /// <param name="searchBufferSize">Search window size</param>
        /// <param name="lookAheadBufferSize">Look-ahead buffer size</param>
        /// <returns>Array of compressed tokens</returns>
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
                    byte* windowStart = (byte*)Math.Max((ulong)dataPtr, (ulong)(current - searchBufferSize));
                    byte* lookAheadEnd = (byte*)Math.Min((ulong)(current + lookAheadBufferSize), (ulong)end);
                    int bestOffset = 0;
                    int bestLength = 0;

                    // AVX2 accelerated search
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

        /// <summary>
        /// Calculates match length using AVX2 vector comparisons
        /// </summary>
        /// <param name="src">Pointer to candidate position</param>
        /// <param name="dest">Pointer to current position</param>
        /// <param name="maxLen">Maximum length to compare</param>
        /// <returns>Number of matching bytes</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int SimdMatchLength(byte* src, byte* dest, int maxLen)
        {
            int len = 0;
            int vectorBlocks = maxLen / MaxVectorSize;

            // Process 32-byte blocks with AVX2
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

            // Process remaining bytes
            while (len < maxLen && src[len] == dest[len])
                len++;

            return len;
        }

        /// <summary>
        /// Processes match results and updates compression state
        /// </summary>
        /// <param name="compressed">Token list to update</param>
        /// <param name="current">Current position pointer (updated)</param>
        /// <param name="end">End of buffer pointer</param>
        /// <param name="bestOffset">Best match offset</param>
        /// <param name="bestLength">Best match length</param>
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

        /// <summary>
        /// Fallback compression implementation using pointer arithmetic
        /// </summary>
        /// <param name="data">Input data to compress</param>
        /// <param name="searchBufferSize">Search window size</param>
        /// <param name="lookAheadBufferSize">Look-ahead buffer size</param>
        /// <returns>Array of compressed tokens</returns>
        private static unsafe Lz77Token[] EncodeFallback(
            byte[] data, int searchBufferSize, int lookAheadBufferSize)
        {
            var compressed = new List<Lz77Token>();
            fixed (byte* dataPtr = data)
            {
                byte* current = dataPtr;
                byte* end = dataPtr + data.Length;

                while (current < end)
                {
                    byte* windowStart = (byte*)Math.Max((ulong)dataPtr, (ulong)(current - searchBufferSize));
                    byte* lookAheadEnd = (byte*)Math.Min((ulong)(current + lookAheadBufferSize), (ulong)end);
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

        /// <summary>
        /// Decompresses LZ77 tokenized data with memory-copy optimizations
        /// </summary>
        /// <param name="tokens">Array of Lz77Token structures</param>
        /// <returns>Decompressed byte array</returns>
        /// <remarks>
        /// Uses AVX-accelerated memory copying when possible, with optimized 
        /// register copying for small blocks. Pre-calculates output size for 
        /// single-allocation efficiency.
        /// </remarks>
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

        /// <summary>
        /// Pre-calculates decompressed data length
        /// </summary>
        /// <param name="tokens">Compressed token array</param>
        /// <returns>Total decompressed size in bytes</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculateOutputLength(Lz77Token[] tokens)
        {
            int length = 0;
            foreach (var token in tokens)
                length += token.Length + (token.NextByte != 0 || token.Length == 0 ? 1 : 0);
            return length;
        }

        /// <summary>
        /// Optimized memory copy with AVX/register acceleration
        /// </summary>
        /// <param name="dest">Destination pointer</param>
        /// <param name="src">Source pointer</param>
        /// <param name="count">Number of bytes to copy</param>
        /// <remarks>
        /// Uses:
        /// - 64-bit register copies for 8-byte blocks
        /// - AVX vector copies for 32-byte blocks
        /// - Byte-by-byte copies for trailing bytes
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MemCopyInline(byte* dest, byte* src, int count)
        {
            if (count <= 0) return;

            // Small blocks: register copying
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

            // Large blocks: AVX vector copying
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

            // Remaining bytes
            while (count >= 8)
            {
                *(ulong*)dest = *(ulong*)src;
                dest += 8;
                src += 8;
                count -= 8;
            }
            while (count-- > 0) *dest++ = *src++;
        }

        /// <summary>
        /// Scans for first set bit in 32-bit value
        /// </summary>
        /// <param name="value">Input value to scan</param>
        /// <returns>Zero-based index of first set bit</returns>
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
