#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace QingYi.Core.Compression.Unsafe.LZMA
{
    internal unsafe class RangeDecoder
    {
        private const uint kTopValue = 1 << 24;
        private uint _code;
        private uint _range;
        private Stream _stream;
        private bool _finished;

        public bool IsFinished => _finished;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Init(Stream stream)
        {
            _stream = stream;
            _range = 0xFFFFFFFF;
            _code = 0;
            _finished = false;

            // 读取前4个字节
            for (int i = 0; i < 5; i++)
            {
                _code = (_code << 8) | (uint)ReadByte();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint DecodeBit(uint* prob)
        {
            uint bound = (_range >> 11) * *prob;
            uint symbol;

            if (_code < bound)
            {
                _range = bound;
                *prob += (2048 - *prob) >> 5;
                symbol = 0;
            }
            else
            {
                _code -= bound;
                _range -= bound;
                *prob -= *prob >> 5;
                symbol = 1;
            }

            Normalize();
            return symbol;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint DecodeTree(uint* probs, int offset, int numBits)
        {
            uint symbol = 1;
            for (int i = 0; i < numBits; i++)
            {
                symbol = (symbol << 1) | DecodeBit(&probs[offset + symbol]);
            }
            return symbol - (1U << numBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint DecodeDirectBits(int numBits)
        {
            uint result = 0;
            for (int i = 0; i < numBits; i++)
            {
                _range >>= 1;
                uint bit = (_code - _range) >> 31;
                _code -= _range & (bit - 1);
                result = (result << 1) | (1 - bit);
                Normalize();
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint DecodeDirectBits(uint* probs, int numBits)
        {
            uint result = 0;
            for (int i = 0; i < numBits; i++)
            {
                result = (result << 1) | DecodeBit(&probs[i]);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Normalize()
        {
            if (_range < kTopValue)
            {
                _range <<= 8;
                _code = (_code << 8) | (uint)ReadByte();
            }
        }

        private int ReadByte()
        {
            int b = _stream.ReadByte();
            if (b == -1)
            {
                _finished = true;
                b = 0;
            }
            return b;
        }
    }
}
#endif