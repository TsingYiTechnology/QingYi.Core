#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QingYi.Core.Compression.Unsafe.LZMA
{
    public class LzmaCompressor : IDisposable
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

        public LzmaCompressor(
            int compressionLevel = 5,
            int numThreads = 0,
            int dictionarySize = 1 << 24,
            int wordSize = 32,
            bool solid = false,
            int solidBlockSize = 1 << 28)
        {
            if (dictionarySize < 1 << 16 || dictionarySize > 1 << 32)
                throw new ArgumentOutOfRangeException(nameof(dictionarySize), "字典大小必须在64KB-4096MB之间");
            if (wordSize < 8 || wordSize > 256)
                throw new ArgumentOutOfRangeException(nameof(wordSize), "单词大小必须在8-256之间");

            _dictionarySize = dictionarySize;
            _numFastBytes = wordSize;
            _matchFinder = compressionLevel >= 5 ? 1 : 0;
            _numThreads = numThreads == 0 ? Environment.ProcessorCount : numThreads;
            _solid = solid;
            _solidBlockSize = solidBlockSize;
        }

        public void Compress(Stream input, Stream output)
        {
            using var encoder = new LzmaEncoder(
                dictionarySize: _dictionarySize,
                numFastBytes: _numFastBytes,
                matchFinder: _matchFinder,
                numThreads: _numThreads,
                solid: _solid,
                solidBlockSize: _solidBlockSize);

            encoder.Code(input, output);
        }

        public void Dispose() { }
    }
}
#endif