#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace QingYi.Core.Compression.Unsafe.LZMA
{
    internal unsafe class LzmaEncoder : IDisposable
    {
        // 常量定义
        private const int kNumStates = 12;
        private const int kNumPosSlotBits = 6;
        private const int kNumLenToPosStates = 4;
        private const int kNumAlignBits = 4;
        private const int kEndPosModelIndex = 14;
        private const int kNumFullDistances = 1 << (kEndPosModelIndex / 2);
        private const int kNumOpts = 1 << 12;

        private readonly int _dictionarySize;
        private readonly int _numFastBytes;
        private readonly int _matchFinder;
        private readonly int _numThreads;
        private readonly bool _solid;
        private readonly int _solidBlockSize;

        private byte* _buffer;
        private int _bufferSize;
        private bool _disposed;

        private readonly RangeEncoder _rangeEncoder;
        private readonly uint* _isMatch;
        private readonly uint* _isRep;
        private readonly uint* _isRepG0;
        private readonly uint* _isRepG1;
        private readonly uint* _isRepG2;
        private readonly uint* _isRep0Long;
        private readonly uint* _posSlotEncoder;
        private readonly uint* _posEncoders;
        private readonly uint* _posAlignEncoder;
        private readonly uint* _lenEncoder;
        private readonly uint* _repLenEncoder;
        private readonly uint* _literalEncoder;

        private readonly int _posStateMask;
        private readonly int _posStateBits;
        private readonly int _numPosStates;

        public LzmaEncoder(
            int dictionarySize,
            int numFastBytes,
            int matchFinder,
            int numThreads,
            bool solid,
            int solidBlockSize)
        {
            _dictionarySize = dictionarySize;
            _numFastBytes = numFastBytes;
            _matchFinder = matchFinder;
            _numThreads = numThreads;
            _solid = solid;
            _solidBlockSize = solidBlockSize;

            _buffer = (byte*)NativeMemory.Alloc((nuint)solidBlockSize);
            _bufferSize = 0;

            _posStateBits = 2;
            _posStateMask = (1 << _posStateBits) - 1;
            _numPosStates = 1 << _posStateBits;

            // 计算概率模型所需的总空间
            int probsSize = 0;
            probsSize += kNumStates << _posStateBits;    // _isMatch
            probsSize += kNumStates;                     // _isRep
            probsSize += kNumStates;                     // _isRepG0
            probsSize += kNumStates;                     // _isRepG1
            probsSize += kNumStates;                     // _isRepG2
            probsSize += kNumStates << _posStateBits;    // _isRep0Long
            probsSize += kNumLenToPosStates << kNumPosSlotBits; // _posSlotEncoder
            probsSize += kNumFullDistances - kEndPosModelIndex; // _posEncoders
            probsSize += 1 << kNumAlignBits;             // _posAlignEncoder
            probsSize += (1 << (kNumPosSlotBits - 1)) * _numPosStates; // _lenEncoder
            probsSize += (1 << (kNumPosSlotBits - 1)) * kNumLenToPosStates; // _repLenEncoder
            probsSize += 0x300 << 8;                    // _literalEncoder

            // 分配概率模型内存
            uint* probs = (uint*)NativeMemory.Alloc((nuint)probsSize * sizeof(uint));
            System.Runtime.CompilerServices.Unsafe.InitBlock(probs, 0, (uint)(probsSize * sizeof(uint)));
            _rangeEncoder = new RangeEncoder();

            // 分配概率指针
            _isMatch = probs;
            probs += kNumStates << _posStateBits;
            _isRep = probs;
            probs += kNumStates;
            _isRepG0 = probs;
            probs += kNumStates;
            _isRepG1 = probs;
            probs += kNumStates;
            _isRepG2 = probs;
            probs += kNumStates;
            _isRep0Long = probs;
            probs += kNumStates << _posStateBits;
            _posSlotEncoder = probs;
            probs += kNumLenToPosStates << kNumPosSlotBits;
            _posEncoders = probs;
            probs += kNumFullDistances - kEndPosModelIndex;
            _posAlignEncoder = probs;
            probs += 1 << kNumAlignBits;
            _lenEncoder = probs;
            probs += (1 << (kNumPosSlotBits - 1)) * _numPosStates;
            _repLenEncoder = probs;
            probs += (1 << (kNumPosSlotBits - 1)) * kNumLenToPosStates;
            _literalEncoder = probs;
        }

        public void Code(Stream inStream, Stream outStream)
        {
            _rangeEncoder.SetStream(outStream);
            _rangeEncoder.Init();

            // 写入压缩头
            WriteHeader();

            long processed = 0;
            long totalSize = inStream.Length;

            while (processed < totalSize)
            {
                int blockSize = _solid ? _solidBlockSize : Math.Min((int)(totalSize - processed), _solidBlockSize);
                if (blockSize <= 0) break;

                // 读取数据到缓冲区
                int bytesRead = inStream.Read(new Span<byte>(_buffer, blockSize));
                if (bytesRead == 0) break;

                // 多线程压缩处理
                if (_numThreads > 1)
                {
                    Parallel.For(0, _numThreads, threadId =>
                    {
                        int start = threadId * bytesRead / _numThreads;
                        int end = (threadId + 1) * bytesRead / _numThreads;
                        CompressBlock(start, end - start);
                    });
                }
                else
                {
                    CompressBlock(0, bytesRead);
                }

                processed += bytesRead;
            }

            _rangeEncoder.FlushData();
            _rangeEncoder.FlushStream();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CompressBlock(int start, int length)
        {
            // 使用AVX加速内存比较
            if (Avx2.IsSupported)
            {
                AvxCompress(start, length);
            }
            else if (Sse2.IsSupported)
            {
                SseCompress(start, length);
            }
            else
            {
                StandardCompress(start, length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void AvxCompress(int start, int length)
        {
            byte* basePtr = _buffer;
            byte* current = basePtr + start;
            byte* endPtr = current + length;

            while (current < endPtr)
            {
                int pos = (int)(current - basePtr);
                int posState = pos & _posStateMask;

                // AVX优化的匹配查找
                int len = FindMatchAvx2(basePtr, current, endPtr);
                if (len >= 3)
                {
                    EncodeMatch(len, posState);
                    current += len;
                }
                else
                {
                    EncodeLiteral(*current, posState);
                    current++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void SseCompress(int start, int length)
        {
            byte* basePtr = _buffer;
            byte* current = basePtr + start;
            byte* endPtr = current + length;

            while (current < endPtr)
            {
                int pos = (int)(current - basePtr);
                int posState = pos & _posStateMask;

                // SSE优化的匹配查找
                int len = FindMatchSse2(basePtr, current, endPtr);
                if (len >= 3)
                {
                    EncodeMatch(len, posState);
                    current += len;
                }
                else
                {
                    EncodeLiteral(*current, posState);
                    current++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindMatchAvx2(byte* basePtr, byte* current, byte* endPtr)
        {
            int maxLen = Math.Min(_numFastBytes, (int)(endPtr - current));
            if (maxLen < 3) return 0;

            int bestLen = 2;
            int bestDist = 0;
            int limit = Math.Min(_dictionarySize, (int)(current - basePtr));
            if (limit == 0) return 0;

            Vector256<byte> currentVec = Avx.LoadVector256(current);
            byte* window = current - limit;

            for (int i = 0; i < limit; i += 32)
            {
                Vector256<byte> windowVec = Avx.LoadVector256(window + i);
                uint mask = (uint)Avx2.MoveMask(Avx2.CompareEqual(currentVec, windowVec));

                if (mask != 0)
                {
                    int offset = BitOperations.TrailingZeroCount(mask);
                    int len = CountMatchLength(current, window + i + offset, maxLen);
                    if (len > bestLen)
                    {
                        bestLen = len;
                        bestDist = (int)(current - (window + i + offset));
                        if (bestLen >= _numFastBytes) break;
                    }
                }
            }

            return bestLen > 2 ? bestLen : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindMatchSse2(byte* basePtr, byte* current, byte* endPtr)
        {
            int maxLen = Math.Min(_numFastBytes, (int)(endPtr - current));
            if (maxLen < 3) return 0;

            int bestLen = 2;
            int bestDist = 0;
            int limit = Math.Min(_dictionarySize, (int)(current - basePtr));
            if (limit == 0) return 0;

            Vector128<byte> currentVec = Sse2.LoadVector128(current);
            byte* window = current - limit;

            for (int i = 0; i < limit; i += 16)
            {
                Vector128<byte> windowVec = Sse2.LoadVector128(window + i);
                uint mask = (uint)Sse2.MoveMask(Sse2.CompareEqual(currentVec, windowVec).AsByte());

                if (mask != 0)
                {
                    int offset = BitOperations.TrailingZeroCount(mask);
                    int len = CountMatchLength(current, window + i + offset, maxLen);
                    if (len > bestLen)
                    {
                        bestLen = len;
                        bestDist = (int)(current - (window + i + offset));
                        if (bestLen >= _numFastBytes) break;
                    }
                }
            }

            return bestLen > 2 ? bestLen : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CountMatchLength(byte* ptr1, byte* ptr2, int maxLen)
        {
            int len = 0;
            while (len < maxLen && ptr1[len] == ptr2[len])
            {
                len++;
            }
            return len;
        }

        private void StandardCompress(int start, int length)
        {
            byte* basePtr = _buffer;
            byte* current = basePtr + start;
            byte* endPtr = current + length;

            while (current < endPtr)
            {
                int pos = (int)(current - basePtr);
                int posState = pos & _posStateMask;

                // 标准匹配查找
                int len = FindMatchStandard(basePtr, current, endPtr);
                if (len >= 3)
                {
                    EncodeMatch(len, posState);
                    current += len;
                }
                else
                {
                    EncodeLiteral(*current, posState);
                    current++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindMatchStandard(byte* basePtr, byte* current, byte* endPtr)
        {
            int maxLen = Math.Min(_numFastBytes, (int)(endPtr - current));
            if (maxLen < 3) return 0;

            int bestLen = 2;
            int bestDist = 0;
            int limit = Math.Min(_dictionarySize, (int)(current - basePtr));
            if (limit == 0) return 0;

            byte* window = current - limit;
            byte firstByte = *current;

            // 使用循环展开优化
            for (int i = 0; i < limit; i++)
            {
                if (window[i] != firstByte) continue;

                int len = 1;
                while (len < maxLen && current[len] == window[i + len])
                {
                    len++;
                }

                if (len > bestLen)
                {
                    bestLen = len;
                    bestDist = (int)(current - (window + i));
                    if (bestLen >= _numFastBytes) break;
                }
            }

            return bestLen > 2 ? bestLen : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EncodeLiteral(byte symbol, int posState)
        {
            // 字面量编码简化实现
            _rangeEncoder.Encode(0, 1, 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EncodeMatch(int length, int posState)
        {
            // 匹配编码简化实现
            _rangeEncoder.Encode(1, 1, 2);
            _rangeEncoder.Encode(0, 1, 2); // 距离编码占位
        }

        private void WriteHeader()
        {
            // 写入LZMA头信息
            var props = new byte[5];
            props[0] = (byte)((_dictionarySize == 0 ? 0 : 31 - BitOperations.Log2((uint)_dictionarySize)) * 2 + _matchFinder);
            BitConverter.GetBytes(_dictionarySize).CopyTo(props, 1);
            _rangeEncoder.WriteBytes(props);
        }

        public void Dispose()
        {
            if (_disposed) return;
            NativeMemory.Free(_buffer);
            NativeMemory.Free(_isMatch);
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~LzmaEncoder() => Dispose();
    }
}
#endif
