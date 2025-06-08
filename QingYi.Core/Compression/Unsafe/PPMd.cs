#if NET7_0_OR_GREATER && !BROWSER
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;

namespace QingYi.Core.Compression.Unsafe
{
    /// <summary>
    /// Provides high-performance PPMd compression/decompression using hardware intrinsics (AVX2/SSE2) 
    /// and unsafe memory operations. Optimized for .NET 7+ environments.
    /// </summary>
    /// <remarks>
    /// This implementation uses parallel processing, native memory allocation, and SIMD optimizations.
    /// Not supported in browser environments.
    /// </remarks>
    public class PPMd : IDisposable
    {
        private const int MaxOrder = 16;
        private const int MaxSize = 512 * 1024 * 1024;
        private const int MinSize = 1024 * 1024;
        private const int MinWordSize = 2;
        private const int MaxWordSize = 256;

        private readonly int _order;
        private readonly int _wordSize;
        private readonly int _threadCount;
        private readonly int _dictSize;
        private readonly bool _useAvx;
        private readonly bool _useUnsafe;
        private readonly MemoryPool<byte> _memoryPool;

        /// <summary>
        /// Initializes a new instance of the PPMd compressor/decompressor.
        /// </summary>
        /// <param name="order">Model order (1-16) for prediction context depth. Higher values may improve compression ratio at the cost of memory.</param>
        /// <param name="wordSize">Word size (2-256 bytes) for SIMD-optimized pattern matching.</param>
        /// <param name="threadCount">Number of processing threads. 0 = Environment.ProcessorCount.</param>
        /// <param name="dictSize">Dictionary size (1MB-512MB) for compression context.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when parameters exceed valid ranges.
        /// </exception>
        public PPMd(int order = 6, int wordSize = 16, int threadCount = 0, int dictSize = 8 * 1024 * 1024)
        {
            if (order < 1 || order > MaxOrder)
                throw new ArgumentOutOfRangeException(nameof(order));
            if (wordSize < MinWordSize || wordSize > MaxWordSize)
                throw new ArgumentOutOfRangeException(nameof(wordSize));
            if (dictSize < MinSize || dictSize > MaxSize)
                throw new ArgumentOutOfRangeException(nameof(dictSize));

            _order = order;
            _wordSize = wordSize;
            _threadCount = threadCount == 0 ? Environment.ProcessorCount : threadCount;
            _dictSize = dictSize;
            _useAvx = Avx2.IsSupported;
            _useUnsafe = true;
            _memoryPool = MemoryPool<byte>.Shared;
        }

        /// <summary>
        /// Compresses input data using parallel PPMd processing and writes to output stream.
        /// </summary>
        /// <param name="input">Source stream containing uncompressed data.</param>
        /// <param name="output">Destination stream for compressed data.</param>
        /// <remarks>
        /// Output format: [16-byte header][4-byte chunk size][compressed data]... 
        /// Header format: order(4), wordSize(4), dictSize(4), originalLength(4)
        /// </remarks>
        public void Compress(Stream input, Stream output)
        {
            var header = new byte[16];
            BitConverter.TryWriteBytes(header.AsSpan(0, 4), _order);
            BitConverter.TryWriteBytes(header.AsSpan(4, 4), _wordSize);
            BitConverter.TryWriteBytes(header.AsSpan(8, 4), _dictSize);
            BitConverter.TryWriteBytes(header.AsSpan(12, 4), (int)input.Length);
            output.Write(header);

            var chunkSize = Math.Min(_dictSize, 64 * 1024 * 1024);
            var chunks = new List<Memory<byte>>();

            long bytesRead = 0;
            while (bytesRead < input.Length)
            {
                var size = (int)Math.Min(chunkSize, input.Length - bytesRead);
                var memory = _memoryPool.Rent(size);
                bytesRead += input.Read(memory.Memory.Span);
                chunks.Add(memory.Memory[..size]);
            }

            var compressedChunks = new ConcurrentBag<byte[]>();
            Parallel.ForEach(chunks, new ParallelOptions { MaxDegreeOfParallelism = _threadCount }, chunk =>
            {
                var compressor = new PpmdCore(_order, _wordSize, _dictSize, _useAvx, _useUnsafe);
                var compressed = compressor.Compress(chunk.Span);
                compressedChunks.Add(compressed);
            });

            foreach (var chunk in compressedChunks)
            {
                output.Write(BitConverter.GetBytes(chunk.Length));
                output.Write(chunk);
            }
        }

        /// <summary>
        /// Decompresses input data using parallel PPMd processing and writes to output stream.
        /// </summary>
        /// <param name="input">Source stream containing compressed data.</param>
        /// <param name="output">Destination stream for decompressed data.</param>
        /// <exception cref="InvalidDataException">
        /// Thrown when compressed data format is invalid.
        /// </exception>
        public void Decompress(Stream input, Stream output)
        {
            var header = new byte[16];
            input.ReadExactly(header);
            var order = BitConverter.ToInt32(header, 0);
            var wordSize = BitConverter.ToInt32(header, 4);
            var dictSize = BitConverter.ToInt32(header, 8);
            var originalSize = BitConverter.ToInt32(header, 12);

            var decompressed = new byte[originalSize];
            var position = 0;

            while (position < originalSize)
            {
                var lengthBuffer = new byte[4];
                input.ReadExactly(lengthBuffer);
                var chunkLength = BitConverter.ToInt32(lengthBuffer, 0);

                var compressedChunk = new byte[chunkLength];
                input.ReadExactly(compressedChunk);

                var decompressor = new PpmdCore(order, wordSize, dictSize, _useAvx, _useUnsafe);
                var decompressedChunk = decompressor.Decompress(compressedChunk);

                decompressedChunk.CopyTo(decompressed.AsMemory(position, decompressedChunk.Length));
                position += decompressedChunk.Length;
            }

            output.Write(decompressed);
        }

#pragma warning disable CA1816 // Dispose 方法应调用 SuppressFinalize
        /// <summary>
        /// Releases all unmanaged memory resources used by the compressor.
        /// </summary>
        public void Dispose() => _memoryPool.Dispose();
#pragma warning restore CA1816 // Dispose 方法应调用 SuppressFinalize

        private unsafe class PpmdCore : IDisposable
        {
            private struct Context
            {
                public uint Sum;
                public int Count;
                public byte* Suffix;
                public ushort* Symbols;
                public ushort* Freqs;
            }

            private readonly int _order;
            private readonly int _wordSize;
            private readonly bool _useAvx;
            private readonly bool _useUnsafe;
            private byte* _memory;
            private int _memorySize;
            private Context* _contexts;
            private int _contextCount;
            private byte* _current;
            private int _allocated;

            public PpmdCore(int order, int wordSize, int dictSize, bool useAvx, bool useUnsafe)
            {
                _order = order;
                _wordSize = wordSize;
                _useAvx = useAvx && Avx2.IsSupported;
                _useUnsafe = useUnsafe;
                _memorySize = dictSize;
                _memory = (byte*)NativeMemory.AllocZeroed((nuint)_memorySize);
                _contexts = (Context*)NativeMemory.Alloc((nuint)(dictSize / 256 * sizeof(Context)));
            }

            public byte[] Compress(ReadOnlySpan<byte> input)
            {
                var output = new List<byte>(input.Length);
                var model = InitializeModel();

                fixed (byte* inputPtr = input)
                {
                    var encoder = new RangeEncoder();
                    byte* current = inputPtr;
                    byte* end = inputPtr + input.Length;

                    while (current < end)
                    {
                        int maxOrder = Math.Min(_order, (int)(end - current));
                        EncodeSymbol(ref encoder, model, current, maxOrder);
                        current += _wordSize;
                    }

                    encoder.Flush();
                    output.AddRange(encoder.GetBytes());
                }

                return output.ToArray();
            }

            public byte[] Decompress(byte[] compressed)
            {
                var output = new List<byte>();
                var model = InitializeModel();
                var decoder = new RangeDecoder(compressed);

                while (!decoder.IsFinished)
                {
                    var symbol = DecodeSymbol(ref decoder, model);
                    output.AddRange(symbol);
                }

                return output.ToArray();
            }

            private Context* InitializeModel()
            {
                _allocated = 0;
                _contextCount = 1;
                _contexts[0] = new Context
                {
                    Symbols = (ushort*)Allocate(256 * sizeof(ushort)),
                    Freqs = (ushort*)Allocate(256 * sizeof(ushort)),
                    Count = 256,
                    Sum = 256
                };

                for (int i = 0; i < 256; i++)
                {
                    _contexts[0].Symbols[i] = (ushort)i;
                    _contexts[0].Freqs[i] = 1;
                }

                return &_contexts[0];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private byte* Allocate(int size)
            {
                if (_allocated + size > _memorySize)
                    throw new OutOfMemoryException("PPMd dictionary exhausted");

                byte* ptr = _memory + _allocated;
                _allocated += size;
                return ptr;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private unsafe void EncodeSymbol(ref RangeEncoder encoder, Context* context, byte* data, int maxOrder)
            {
                // 创建当前上下文指针
                Context* currentContext = context;
                int order = 0;
                uint symbol = 0;

                // AVX2加速的上下文匹配
                if (_useAvx && maxOrder >= 8)
                {
                    // 加载256位数据向量
                    Vector256<byte> dataVec = Avx.LoadVector256(data);

                    while (order < maxOrder)
                    {
                        // 计算当前上下文的最大匹配长度
                        int matchLength = 0;
                        Context* nextContext = null;

                        // 使用AVX2比较上下文
                        for (int i = 0; i < currentContext->Count; i++)
                        {
                            byte* ctxData = currentContext->Suffix + i * _wordSize;
                            Vector256<byte> ctxVec = Avx.LoadVector256(ctxData);

                            // 比较数据向量和上下文向量
                            uint mask = (uint)Avx2.MoveMask(Avx2.CompareEqual(dataVec, ctxVec));
                            int len = BitOperations.TrailingZeroCount(mask);

                            if (len > matchLength)
                            {
                                matchLength = len;
                                symbol = (uint)i;
                                nextContext = (Context*)(currentContext->Symbols + i);
                            }
                        }

                        if (matchLength == 0) break;

                        // 编码匹配的符号
                        uint freq = currentContext->Freqs[symbol];
                        uint total = currentContext->Sum;
                        encoder.Encode(symbol, freq, total);

                        // 更新模型统计
                        currentContext->Freqs[symbol]++;
                        currentContext->Sum++;

                        // 移动到下一级上下文
                        currentContext = nextContext;
                        order++;
                        data += matchLength;
                    }
                }

                // 标量回退处理
                while (order < maxOrder)
                {
                    bool found = false;
                    for (uint i = 0; i < currentContext->Count; i++)
                    {
                        byte* ctxData = currentContext->Suffix + i * _wordSize;

                        // 标量比较
                        int matchLength = 0;
                        while (matchLength < _wordSize &&
                               data[matchLength] == ctxData[matchLength])
                        {
                            matchLength++;
                        }

                        if (matchLength > 0)
                        {
                            // 编码匹配的符号
                            uint freq = currentContext->Freqs[i];
                            uint total = currentContext->Sum;
                            encoder.Encode(i, freq, total);

                            // 更新模型统计
                            currentContext->Freqs[i]++;
                            currentContext->Sum++;

                            // 移动到下一级上下文
                            currentContext = (Context*)(currentContext->Symbols + i);
                            order++;
                            data += matchLength;
                            found = true;
                            break;
                        }
                    }

                    if (!found) break;
                }

                // 处理未匹配的字符
                if (order < maxOrder)
                {
                    // 添加转义符号
                    uint escapeFreq = 1;
                    uint total = currentContext->Sum + escapeFreq;
                    encoder.Encode((uint)currentContext->Count, escapeFreq, total);

                    // 更新模型
                    currentContext->Sum += escapeFreq;

                    // 添加新符号到模型
                    if (currentContext->Count < 256)
                    {
                        int index = currentContext->Count;
                        currentContext->Symbols[index] = (ushort)_contextCount;
                        currentContext->Freqs[index] = 1;
                        currentContext->Count++;
                        currentContext->Sum++;

                        // 创建新上下文
                        Context* newContext = &_contexts[_contextCount++];
                        *newContext = new Context
                        {
                            Symbols = (ushort*)Allocate(256 * sizeof(ushort)),
                            Freqs = (ushort*)Allocate(256 * sizeof(ushort)),
                            Suffix = Allocate(_wordSize),
                            Count = 0,
                            Sum = 0
                        };

                        // 复制数据到新上下文
                        MemoryOperations.BlockCopy(data, newContext->Suffix, _wordSize);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private unsafe byte[] DecodeSymbol(ref RangeDecoder decoder, Context* context)
            {
                // 创建当前上下文指针
                Context* currentContext = context;
                byte* output = stackalloc byte[_wordSize];
                int decoded = 0;

                while (decoded < _wordSize)
                {
                    if (currentContext->Count == 0)
                    {
                        // 转义情况
                        uint escapeFreq = 1;
                        uint total = currentContext->Sum + escapeFreq;

                        if (decoder.Decode(total) != currentContext->Sum)
                            throw new InvalidDataException("Invalid compressed data");

                        // 需要从更高阶上下文解码
                        break;
                    }

                    // 解码符号
                    uint totalFreq = currentContext->Sum;
                    uint value = decoder.Decode(totalFreq);

                    uint cumulative = 0;
                    uint symbol = 0;
                    bool found = false;

                    // 查找匹配的符号
                    for (uint i = 0; i < currentContext->Count; i++)
                    {
                        cumulative += currentContext->Freqs[i];
                        if (value < cumulative)
                        {
                            symbol = i;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        throw new InvalidDataException("Invalid compressed data");

                    // 更新模型
                    currentContext->Freqs[symbol]++;
                    currentContext->Sum++;

                    // 获取符号对应的数据
                    byte* symbolData = currentContext->Suffix + symbol * _wordSize;

                    // 复制解码的数据
                    int copySize = Math.Min(_wordSize - decoded, _wordSize);
                    MemoryOperations.BlockCopy(symbolData, output + decoded, copySize);
                    decoded += copySize;

                    // 移动到下一级上下文
                    currentContext = (Context*)(currentContext->Symbols + symbol);
                }

                // 处理转义情况
                if (decoded < _wordSize)
                {
                    // 从父上下文解码剩余部分
                    byte[] remaining = new byte[_wordSize - decoded];
                    for (int i = 0; i < remaining.Length; i++)
                    {
                        // 简单模型解码单个字节
                        Context* byteContext = context;
                        uint totalFreq = byteContext->Sum;
                        uint value = decoder.Decode(totalFreq);

                        uint cumulative = 0;
                        for (uint j = 0; j < byteContext->Count; j++)
                        {
                            cumulative += byteContext->Freqs[j];
                            if (value < cumulative)
                            {
                                output[decoded++] = (byte)j;
                                byteContext->Freqs[j]++;
                                byteContext->Sum++;
                                break;
                            }
                        }
                    }
                }

                // 返回解码的单词
                byte[] result = new byte[_wordSize];
                Marshal.Copy((IntPtr)output, result, 0, _wordSize);
                return result;
            }

            public void Dispose()
            {
                if (_memory != null)
                {
                    NativeMemory.Free(_memory);
                    _memory = null;
                }
                if (_contexts != null)
                {
                    NativeMemory.Free(_contexts);
                    _contexts = null;
                }
            }
        }

        private ref struct RangeEncoder
        {
            private ulong _low;
            private uint _range;
            private byte _cache;
            private int _cacheSize;
            private List<byte> _output;

            public RangeEncoder()
            {
                _low = 0;
                _range = 0xFFFFFFFF;
                _cache = 0;
                _cacheSize = 1;
                _output = new List<byte>(1024);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Encode(uint start, uint freq, uint total)
            {
                _range /= total;
                _low += start * _range;
                _range *= freq;

                while ((_low ^ (_low + _range)) < 0x1000000 || _range < 0x10000)
                {
                    if (_range < 0x10000)
                    {
                        _range = (uint)((0xFFFF & -((int)_low >> 24)) << 8);
                    }
                    WriteByte((byte)(_low >> 24));
                    _range <<= 8;
                    _low <<= 8;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteByte(byte value)
            {
                if (value != 0xFF)
                {
                    if (_cacheSize > 0)
                    {
                        _output.Add(_cache);
                        while (--_cacheSize > 0)
                            _output.Add(0xFF);
                    }
                    _cache = value;
                }
                else
                {
                    _cacheSize++;
                }
            }

            public void Flush()
            {
                for (int i = 0; i < 4; i++)
                {
                    WriteByte((byte)(_low >> 24));
                    _low <<= 8;
                }
            }

            public byte[] GetBytes() => _output.ToArray();
        }

        private ref struct RangeDecoder
        {
            private readonly ReadOnlySpan<byte> _input;
            private int _position;
            private uint _code;
            private uint _range;

            public RangeDecoder(byte[] input)
            {
                _input = input;
                _position = 0;
                _code = 0;
                _range = 0xFFFFFFFF;

                for (int i = 0; i < 4; i++)
                {
                    _code = (_code << 8) | (_position < _input.Length ? _input[_position++] : (byte)0);
                }
            }

            public bool IsFinished => _position >= _input.Length;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public uint Decode(uint total)
            {
                _range /= total;
                uint value = _code / _range;
                _code -= value * _range;

                while (_range < 0x10000)
                {
                    _range <<= 8;
                    _code = (_code << 8) | (_position < _input.Length ? _input[_position++] : (byte)0);
                }

                return value;
            }
        }

        // IL Emitted optimized memory operations
        private static class MemoryOperations
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe void BlockCopy(byte* src, byte* dest, int count)
            {
                // AVX2优化的大块复制
                if (Avx2.IsSupported && count >= 256)
                {
                    int vector256Size = 256 / 8;
                    int blocks = count / vector256Size;
                    int remainder = count % vector256Size;

                    for (int i = 0; i < blocks; i++)
                    {
                        Vector256<byte> v0 = Avx.LoadVector256(src + i * vector256Size);
                        Vector256<byte> v1 = Avx.LoadVector256(src + i * vector256Size + 32);
                        Vector256<byte> v2 = Avx.LoadVector256(src + i * vector256Size + 64);
                        Vector256<byte> v3 = Avx.LoadVector256(src + i * vector256Size + 96);
                        Vector256<byte> v4 = Avx.LoadVector256(src + i * vector256Size + 128);
                        Vector256<byte> v5 = Avx.LoadVector256(src + i * vector256Size + 160);
                        Vector256<byte> v6 = Avx.LoadVector256(src + i * vector256Size + 192);
                        Vector256<byte> v7 = Avx.LoadVector256(src + i * vector256Size + 224);

                        Avx.Store(dest + i * vector256Size, v0);
                        Avx.Store(dest + i * vector256Size + 32, v1);
                        Avx.Store(dest + i * vector256Size + 64, v2);
                        Avx.Store(dest + i * vector256Size + 96, v3);
                        Avx.Store(dest + i * vector256Size + 128, v4);
                        Avx.Store(dest + i * vector256Size + 160, v5);
                        Avx.Store(dest + i * vector256Size + 192, v6);
                        Avx.Store(dest + i * vector256Size + 224, v7);
                    }

                    src += blocks * vector256Size;
                    dest += blocks * vector256Size;
                    count = remainder;
                }

                // SSE优化中等块复制
                if (Sse2.IsSupported && count >= 128)
                {
                    int vector128Size = 128 / 8;
                    int blocks = count / vector128Size;
                    int remainder = count % vector128Size;

                    for (int i = 0; i < blocks; i++)
                    {
                        Vector128<byte> v0 = Sse2.LoadVector128(src + i * vector128Size);
                        Vector128<byte> v1 = Sse2.LoadVector128(src + i * vector128Size + 16);
                        Vector128<byte> v2 = Sse2.LoadVector128(src + i * vector128Size + 32);
                        Vector128<byte> v3 = Sse2.LoadVector128(src + i * vector128Size + 48);
                        Vector128<byte> v4 = Sse2.LoadVector128(src + i * vector128Size + 64);
                        Vector128<byte> v5 = Sse2.LoadVector128(src + i * vector128Size + 80);
                        Vector128<byte> v6 = Sse2.LoadVector128(src + i * vector128Size + 96);
                        Vector128<byte> v7 = Sse2.LoadVector128(src + i * vector128Size + 112);

                        Sse2.Store(dest + i * vector128Size, v0);
                        Sse2.Store(dest + i * vector128Size + 16, v1);
                        Sse2.Store(dest + i * vector128Size + 32, v2);
                        Sse2.Store(dest + i * vector128Size + 48, v3);
                        Sse2.Store(dest + i * vector128Size + 64, v4);
                        Sse2.Store(dest + i * vector128Size + 80, v5);
                        Sse2.Store(dest + i * vector128Size + 96, v6);
                        Sse2.Store(dest + i * vector128Size + 112, v7);
                    }

                    src += blocks * vector128Size;
                    dest += blocks * vector128Size;
                    count = remainder;
                }

                // 64位大块复制
                while (count >= 8)
                {
                    *(ulong*)dest = *(ulong*)src;
                    src += 8;
                    dest += 8;
                    count -= 8;
                }

                // 32位复制
                if (count >= 4)
                {
                    *(uint*)dest = *(uint*)src;
                    src += 4;
                    dest += 4;
                    count -= 4;
                }

                // 16位复制
                if (count >= 2)
                {
                    *(ushort*)dest = *(ushort*)src;
                    src += 2;
                    dest += 2;
                    count -= 2;
                }

                // 单字节复制
                if (count > 0)
                {
                    *dest = *src;
                }
            }
        }
    }
}
#endif