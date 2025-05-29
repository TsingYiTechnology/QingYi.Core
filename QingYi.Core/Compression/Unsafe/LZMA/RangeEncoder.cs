#if NET8_0_OR_GREATER
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace QingYi.Core.Compression.Unsafe.LZMA
{
    internal unsafe class RangeEncoder
    {
        private const int kTopMask = ~((1 << 24) - 1);
        private const int kNumBitModelTotalBits = 11;
        private const int kBitModelTotal = 1 << kNumBitModelTotalBits;
        private const int kNumMoveBits = 5;

        private Stream _stream;
        private ulong _low;
        private uint _range;
        private byte _cache;
        private int _cacheSize;

        public void SetStream(Stream stream) => _stream = stream;

        public void Init()
        {
            _low = 0;
            _range = 0xFFFFFFFF;
            _cache = 0;
            _cacheSize = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(uint start, uint size, uint total)
        {
            _low += start * (_range /= total);
            _range *= size;
            Normalize();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Normalize()
        {
            while (_range < (1 << 24))
            {
                _range <<= 8;
                ShiftLow();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ShiftLow()
        {
            if ((uint)_low < 0xFF000000 || (uint)(_low >> 32) == 1)
            {
                byte temp = _cache;
                do
                {
                    _stream.WriteByte((byte)(temp + (_low >> 32)));
                    temp = 0xFF;
                } while (--_cacheSize != 0);
                _cache = (byte)((uint)_low >> 24);
            }
            _cacheSize++;
            _low = (uint)_low << 8;
        }

        public void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            foreach (byte b in bytes)
            {
                _stream.WriteByte(b);
            }
        }

        public void FlushData()
        {
            for (int i = 0; i < 5; i++)
                ShiftLow();
        }

        public void FlushStream() => _stream.Flush();
    }

}
#endif