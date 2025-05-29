#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace QingYi.Core.Compression.Unsafe.LZMA
{
    internal unsafe class LzmaDecoder : IDisposable
    {
        // 常量定义
        private const int kNumStates = 12;
        private const int kNumPosSlotBits = 6;
        private const int kNumLenToPosStates = 4;
        private const int kNumAlignBits = 4;
        private const int kEndPosModelIndex = 14;
        private const int kNumFullDistances = 1 << (kEndPosModelIndex / 2);

        private uint _dictionarySize;
        private int _lc, _lp, _pb;
        private uint _posStateMask;
        private uint _literalPosMask;

        private byte* _dictionary;
        private uint _dictionaryPos;
        private bool _disposed;

        private RangeDecoder _rangeDecoder;
        private uint* _isMatch;
        private uint* _isRep;
        private uint* _isRepG0;
        private uint* _isRepG1;
        private uint* _isRepG2;
        private uint* _isRep0Long;
        private uint* _posSlotDecoder;
        private uint* _posDecoders;
        private uint* _posAlignDecoder;
        private uint* _lenDecoder;
        private uint* _repLenDecoder;
        private uint* _literalDecoder;

        private uint _state;
        private byte _previousByte;
        private readonly uint[] _repDistances = new uint[4];

        public LzmaDecoder()
        {
            // 初始化默认值
            _state = 0;
            _previousByte = 0;
            Array.Fill<uint>(_repDistances, 0); // 修复: 显式指定类型参数
        }

        public void SetProperties(int lc, int lp, int pb, uint dictionarySize)
        {
            if (lc > 8 || lp > 4 || pb > 4 || dictionarySize < (1 << 16) || dictionarySize > (1U << 31))
                throw new ArgumentException("Invalid LZMA properties");

            _lc = lc;
            _lp = lp;
            _pb = pb;
            _dictionarySize = dictionarySize;
            _posStateMask = (1U << _pb) - 1;
            _literalPosMask = (1U << _lp) - 1;

            InitializeStructures();
        }

        private void InitializeStructures()
        {
            // 释放之前的资源
            ReleaseMemory();

            // 分配字典
            _dictionary = (byte*)NativeMemory.Alloc((nuint)_dictionarySize);
            _dictionaryPos = 0;

            // 计算概率模型所需的总空间
            int probsSize = 0;
            probsSize += kNumStates << (int)_posStateMask;   // _isMatch
            probsSize += kNumStates;                         // _isRep
            probsSize += kNumStates;                         // _isRepG0
            probsSize += kNumStates;                         // _isRepG1
            probsSize += kNumStates;                         // _isRepG2
            probsSize += kNumStates << (int)_posStateMask;   // _isRep0Long
            probsSize += kNumLenToPosStates << kNumPosSlotBits; // _posSlotDecoder
            probsSize += kNumFullDistances - kEndPosModelIndex; // _posDecoders
            probsSize += 1 << kNumAlignBits;                 // _posAlignDecoder
            probsSize += (1 << (kNumPosSlotBits - 1)) * (1 << _pb); // _lenDecoder
            probsSize += (1 << (kNumPosSlotBits - 1)) * kNumLenToPosStates; // _repLenDecoder
            probsSize += 0x300 << (_lc + _lp);                // _literalDecoder

            // 分配概率模型内存
            uint* probs = (uint*)NativeMemory.Alloc((nuint)probsSize * sizeof(uint));
            System.Runtime.CompilerServices.Unsafe.InitBlock(probs, 0, (uint)(probsSize * sizeof(uint)));

            // 分配概率指针
            _isMatch = probs;
            probs += kNumStates << (int)_posStateMask;
            _isRep = probs;
            probs += kNumStates;
            _isRepG0 = probs;
            probs += kNumStates;
            _isRepG1 = probs;
            probs += kNumStates;
            _isRepG2 = probs;
            probs += kNumStates;
            _isRep0Long = probs;
            probs += kNumStates << (int)_posStateMask;
            _posSlotDecoder = probs;
            probs += kNumLenToPosStates << kNumPosSlotBits;
            _posDecoders = probs;
            probs += kNumFullDistances - kEndPosModelIndex;
            _posAlignDecoder = probs;
            probs += 1 << kNumAlignBits;
            _lenDecoder = probs;
            probs += (1 << (kNumPosSlotBits - 1)) * (1 << _pb);
            _repLenDecoder = probs;
            probs += (1 << (kNumPosSlotBits - 1)) * kNumLenToPosStates;
            _literalDecoder = probs;

            // 初始化范围解码器
            _rangeDecoder = new RangeDecoder();
        }

        public void Code(Stream input, Stream output)
        {
            _rangeDecoder.Init(input);

            while (true)
            {
                uint posState = _dictionaryPos & _posStateMask;

                // 修复: 使用指针算术而不是索引器
                uint* isMatchPtr = _isMatch + (_state << (int)_posStateMask) + posState;
                if (_rangeDecoder.DecodeBit(isMatchPtr) == 0)
                {
                    // 字面量
                    byte symbol = DecodeLiteral();
                    _dictionary[_dictionaryPos++] = symbol;
                    output.WriteByte(symbol);
                    _previousByte = symbol;
                    _state = UpdateStateLiteral(_state);
                }
                else
                {
                    // 匹配
                    uint len;
                    // 修复: 使用指针算术
                    if (_rangeDecoder.DecodeBit(_isRep + _state) != 0)
                    {
                        // 重复匹配
                        len = DecodeRepMatch();
                    }
                    else
                    {
                        // 新匹配
                        len = DecodeMatch();
                    }

                    // 复制匹配数据
                    uint distance = _repDistances[0];
                    uint srcPos = (_dictionaryPos - distance - 1) % _dictionarySize; // 修复: 使用模运算

                    for (uint i = 0; i < len; i++)
                    {
                        byte symbol = _dictionary[srcPos];
                        _dictionary[_dictionaryPos] = symbol;
                        output.WriteByte(symbol);

                        _dictionaryPos = (_dictionaryPos + 1) % _dictionarySize; // 修复: 使用模运算
                        srcPos = (srcPos + 1) % _dictionarySize; // 修复: 使用模运算
                    }

                    _previousByte = _dictionary[(_dictionaryPos - 1) % _dictionarySize]; // 修复: 使用模运算
                }

                // 检查流结束
                if (_rangeDecoder.IsFinished)
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte DecodeLiteral()
        {
            uint symbol = 1;
            uint context = 1;
            uint literalContext = (uint)(_previousByte >> (8 - _lc));
            uint literalPosState = (_dictionaryPos & _literalPosMask) << _lc;
            uint probsOffset = literalContext + literalPosState;

            for (int i = 7; i >= 0; i--)
            {
                // 修复: 使用指针算术
                uint* probPtr = _literalDecoder + probsOffset + context;
                uint bit = _rangeDecoder.DecodeBit(probPtr);
                context = (context << 1) | bit;
                symbol = (symbol << 1) | bit;

                if (context >= 0x100)
                    break;
            }

            return (byte)symbol;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint DecodeRepMatch()
        {
            uint len;

            // 修复: 使用指针算术
            if (_rangeDecoder.DecodeBit(_isRepG0 + _state) == 0)
            {
                // rep0
                uint* isRep0LongPtr = _isRep0Long + (_state << (int)_posStateMask) + (_dictionaryPos & _posStateMask);
                if (_rangeDecoder.DecodeBit(isRep0LongPtr) == 0)
                {
                    // 短重复匹配 (1字节)
                    _state = (_state < 7) ? (uint)9 : (uint)11;
                    return 1;
                }
            }
            else
            {
                uint distance;
                // 修复: 使用指针算术
                if (_rangeDecoder.DecodeBit(_isRepG1 + _state) == 0)
                {
                    distance = _repDistances[1];
                }
                else
                {
                    // 修复: 使用指针算术
                    if (_rangeDecoder.DecodeBit(_isRepG2 + _state) == 0)
                    {
                        distance = _repDistances[2];
                    }
                    else
                    {
                        distance = _repDistances[3];
                        _repDistances[3] = _repDistances[2];
                    }
                    _repDistances[2] = _repDistances[1];
                }
                _repDistances[1] = _repDistances[0];
                _repDistances[0] = distance;
            }

            len = DecodeLength(_repLenDecoder);
            _state = (_state < 7) ? (uint)8 : (uint)11;
            return len;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint DecodeMatch()
        {
            // 更新重复距离缓冲区
            _repDistances[3] = _repDistances[2];
            _repDistances[2] = _repDistances[1];
            _repDistances[1] = _repDistances[0];

            // 解码距离
            _repDistances[0] = DecodeDistance();

            // 解码长度
            uint len = DecodeLength(_lenDecoder);
            _state = (_state < 7) ? (uint)7 : (uint)10;
            return len;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint DecodeDistance()
        {
            uint lenState = (_dictionaryPos < 256) ? (uint)0 :
                (_dictionaryPos < 512) ? (uint)1 :
                (_dictionaryPos < 1024) ? (uint)2 : (uint)3;

            // 解码位置槽
            uint slot = _rangeDecoder.DecodeTree(_posSlotDecoder, (int)lenState * (1 << kNumPosSlotBits), kNumPosSlotBits);

            if (slot < 4)
                return slot + 1;

            uint footerBits = (slot >> 1) - 1;
            uint distance = (2 | (slot & 1)) << (int)footerBits;

            if (slot < kEndPosModelIndex)
            {
                // 直接解码
                uint probsIndex = distance - slot - 1;
                // 修复: 使用指针算术
                distance += _rangeDecoder.DecodeDirectBits(_posDecoders + probsIndex, (int)footerBits);
            }
            else
            {
                // 对齐位解码
                // 修复: 显式转换
                distance += _rangeDecoder.DecodeDirectBits((int)footerBits - kNumAlignBits) << kNumAlignBits;
                distance += _rangeDecoder.DecodeTree(_posAlignDecoder, 0, kNumAlignBits);
            }

            return distance + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint DecodeLength(uint* probs)
        {
            uint lenState = _dictionaryPos & _posStateMask;

            // 修复: 使用指针算术
            if (_rangeDecoder.DecodeBit(probs + lenState) == 0)
            {
                // 短长度 (2-9字节)
                return 2 + _rangeDecoder.DecodeTree(probs + 1, 0, 3);
            }

            // 修复: 使用指针算术
            if (_rangeDecoder.DecodeBit(probs + lenState + 0x100) == 0)
            {
                // 中长度 (10-17字节)
                return 8 + 2 + _rangeDecoder.DecodeTree(probs + 0x101, 0, 3);
            }

            // 长长度 (18-273字节)
            return 16 + 2 + _rangeDecoder.DecodeDirectBits(8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint UpdateStateLiteral(uint state)
        {
            if (state < 4) return 0;
            if (state < 10) return state - 3;
            return state - 6;
        }

        private void ReleaseMemory()
        {
            if (_dictionary != null)
            {
                NativeMemory.Free(_dictionary);
                _dictionary = null;
            }

            if (_isMatch != null)
            {
                NativeMemory.Free(_isMatch);
                _isMatch = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            ReleaseMemory();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~LzmaDecoder() => Dispose();
    }
}
#endif