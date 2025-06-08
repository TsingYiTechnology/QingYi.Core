#if NET6_0_OR_GREATER && !BROWSER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QingYi.Core.Compression
{
    /// <summary>
    /// Provides PPMd compression and decompression functionality with support for parallel processing.
    /// Implements the <see cref="IDisposable"/> interface for resource cleanup.
    /// </summary>
    public class PPMd : IDisposable
    {
        private const int HeaderSize = 10;

        /// <summary>
        /// Gets the compression level (1-12) used by the PPMd algorithm.
        /// </summary>
        public int CompressionLevel { get; }

        /// <summary>
        /// Gets the number of threads used for parallel compression/decompression.
        /// </summary>
        public int ThreadCount { get; }

        /// <summary>
        /// Gets the dictionary size in bytes (1MB-512MB) used for compression.
        /// </summary>
        public int DictionarySize { get; }

        /// <summary>
        /// Gets the model order (2-16) controlling context length for predictions.
        /// </summary>
        public int ModelOrder { get; }

        private readonly bool _parallelExecution;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the PPMd compressor/decompressor.
        /// </summary>
        /// <param name="compressionLevel">Compression level (1-12). Default is 6.</param>
        /// <param name="threadCount">
        /// Number of processing threads. Default (0) uses Environment.ProcessorCount.
        /// </param>
        /// <param name="dictionarySize">
        /// Dictionary size in bytes (1MB-512MB). Default is 16MB.
        /// </param>
        /// <param name="modelOrder">
        /// Model order (2-16) for prediction contexts. Default is 6.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when modelOrder or dictionarySize are outside valid ranges.
        /// </exception>
        public PPMd(int compressionLevel = 6, int threadCount = 0, int dictionarySize = 16 * 1024 * 1024, int modelOrder = 6)
        {
            if (modelOrder < 2 || modelOrder > 16)
                throw new ArgumentException("Model order must be between 2-16", nameof(modelOrder));

            if (dictionarySize < 1024 * 1024 || dictionarySize > 512 * 1024 * 1024)
                throw new ArgumentException("Dictionary size must be between 1MB-512MB", nameof(dictionarySize));

            CompressionLevel = Math.Clamp(compressionLevel, 1, 12);
            ThreadCount = threadCount <= 0 ? Environment.ProcessorCount : threadCount;
            DictionarySize = dictionarySize;
            ModelOrder = modelOrder;
            _parallelExecution = ThreadCount > 1;
        }

        /// <summary>
        /// Compresses data from the input stream and writes it to the output stream.
        /// </summary>
        /// <param name="input">Stream containing uncompressed data.</param>
        /// <param name="output">Stream to receive compressed data.</param>
        public void Compress(Stream input, Stream output)
        {
            WriteHeader(output);

            if (_parallelExecution)
                ParallelCompress(input, output);
            else
                SingleThreadCompress(input, output);
        }

        /// <summary>
        /// Decompresses data from the input stream and writes it to the output stream.
        /// </summary>
        /// <param name="input">Stream containing compressed data.</param>
        /// <param name="output">Stream to receive decompressed data.</param>
        /// <exception cref="InvalidDataException">
        /// Thrown for invalid headers or stream corruption.
        /// </exception>
        public void Decompress(Stream input, Stream output)
        {
            ReadHeader(input);

            if (_parallelExecution)
                ParallelDecompress(input, output);
            else
                SingleThreadDecompress(input, output);
        }

        private void WriteHeader(Stream output)
        {
            var header = new byte[HeaderSize];
            header[0] = (byte)CompressionLevel;
            header[1] = (byte)ThreadCount;
            BitConverter.GetBytes(DictionarySize).CopyTo(header, 2);
            header[6] = (byte)ModelOrder;
            // Reserved bytes for future use
            header[7] = 0;
            header[8] = 0;
            header[9] = 0;
            output.Write(header, 0, HeaderSize);
        }

        private void ReadHeader(Stream input)
        {
            var header = new byte[HeaderSize];
            if (input.Read(header, 0, HeaderSize) != HeaderSize)
                throw new InvalidDataException("Invalid PPMd header");

            int level = header[0];
            int threads = header[1];
            int dictSize = BitConverter.ToInt32(header, 2);
            int order = header[6];

            if (level != CompressionLevel || threads != ThreadCount ||
                dictSize != DictionarySize || order != ModelOrder)
            {
                throw new InvalidDataException("Compressor parameters do not match header");
            }
        }

        private void SingleThreadCompress(Stream input, Stream output)
        {
            using var engine = new PPMdEngine(CompressionLevel, DictionarySize, ModelOrder);
            engine.Compress(input, output);
        }

        private void SingleThreadDecompress(Stream input, Stream output)
        {
            using var engine = new PPMdEngine(CompressionLevel, DictionarySize, ModelOrder);
            engine.Decompress(input, output);
        }

        private void ParallelCompress(Stream input, Stream output)
        {
            var chunkQueue = new BlockingCollection<(byte[] data, int index)>(ThreadCount * 2);
            var resultQueue = new BlockingCollection<(byte[] data, int index, int origSize)>(ThreadCount * 2);

            // Producer - read input in chunks
            var producer = Task.Run(() =>
            {
                int index = 0;
                byte[] buffer = new byte[DictionarySize];
                int bytesRead;

                while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    var chunk = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);
                    chunkQueue.Add((chunk, index++));
                }
                chunkQueue.CompleteAdding();
            });

            // Consumers - compress chunks
            var compressors = Enumerable.Range(0, ThreadCount).Select(_ => Task.Run(() =>
            {
                using var engine = new PPMdEngine(CompressionLevel, DictionarySize, ModelOrder);

                foreach (var (chunk, index) in chunkQueue.GetConsumingEnumerable())
                {
                    using var memStream = new MemoryStream();
                    engine.Compress(new MemoryStream(chunk), memStream);
                    var compressed = memStream.ToArray();
                    resultQueue.Add((compressed, index, chunk.Length));
                }
            })).ToArray();

            // Writer - write results in order
            var writer = Task.Run(() =>
            {
                var results = new SortedDictionary<int, (byte[] data, int origSize)>();
                int nextIndex = 0;

                foreach (var (data, index, origSize) in resultQueue.GetConsumingEnumerable())
                {
                    results[index] = (data, origSize);

                    while (results.TryGetValue(nextIndex, out var item))
                    {
                        // Write chunk header: [original size][compressed size]
                        output.Write(BitConverter.GetBytes(item.origSize), 0, 4);
                        output.Write(BitConverter.GetBytes(item.data.Length), 0, 4);
                        output.Write(item.data, 0, item.data.Length);

                        results.Remove(nextIndex);
                        nextIndex++;
                    }
                }
            });

            producer.Wait();
            Task.WaitAll(compressors);
            resultQueue.CompleteAdding();
            writer.Wait();
        }

        private void ParallelDecompress(Stream input, Stream output)
        {
            var chunkQueue = new BlockingCollection<(byte[] data, int index, int origSize)>(ThreadCount * 2);
            var resultQueue = new BlockingCollection<(byte[] data, int index)>(ThreadCount * 2);

            // Producer - read compressed chunks
            var producer = Task.Run(() =>
            {
                int index = 0;
                byte[] sizeBuffer = new byte[8];

                while (input.Read(sizeBuffer, 0, 8) == 8)
                {
                    int origSize = BitConverter.ToInt32(sizeBuffer, 0);
                    int compSize = BitConverter.ToInt32(sizeBuffer, 4);

                    var chunk = new byte[compSize];
                    if (input.Read(chunk, 0, compSize) != compSize)
                        throw new InvalidDataException("Unexpected end of stream");

                    chunkQueue.Add((chunk, index++, origSize));
                }
                chunkQueue.CompleteAdding();
            });

            // Consumers - decompress chunks
            var decompressors = Enumerable.Range(0, ThreadCount).Select(_ => Task.Run(() =>
            {
                using var engine = new PPMdEngine(CompressionLevel, DictionarySize, ModelOrder);

                foreach (var (chunk, index, origSize) in chunkQueue.GetConsumingEnumerable())
                {
                    using var inputMem = new MemoryStream(chunk);
                    using var outputMem = new MemoryStream(origSize);
                    engine.Decompress(inputMem, outputMem);
                    resultQueue.Add((outputMem.ToArray(), index));
                }
            })).ToArray();

            // Writer - write results in order
            var writer = Task.Run(() =>
            {
                var results = new SortedDictionary<int, byte[]>();
                int nextIndex = 0;

                foreach (var (data, index) in resultQueue.GetConsumingEnumerable())
                {
                    results[index] = data;

                    while (results.TryGetValue(nextIndex, out var chunk))
                    {
                        output.Write(chunk, 0, chunk.Length);
                        results.Remove(nextIndex);
                        nextIndex++;
                    }
                }
            });

            producer.Wait();
            Task.WaitAll(decompressors);
            resultQueue.CompleteAdding();
            writer.Wait();
        }

        /// <summary>
        /// Releases all resources used by the PPMd instance.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                // Cleanup resources if needed
            }
            GC.SuppressFinalize(this);
        }
    }

    internal sealed class PPMdEngine : IDisposable
    {
        private const int MaxOrder = 16;
        private const int EscapeSymbol = 256;          // 逃逸符号
        private const int BinSymbolLimit = 257;        // 符号范围 (0-256)
        private const int InitialSum = BinSymbolLimit * 4; // 初始频率总和

        private readonly Model _model;
        private readonly RangeCoder _coder;
        private bool _disposed;

        public PPMdEngine(int level, int memSize, int order)
        {
            _model = new Model(Math.Min(order, MaxOrder), memSize, level);
            _coder = new RangeCoder();
        }

        public void Compress(Stream input, Stream output)
        {
            _coder.InitEncode(output);
            _model.StartEncoding();

            int b;
            while ((b = input.ReadByte()) != -1)
            {
                // 明确转换为byte
                _model.EncodeSymbol(_coder, (byte)b);
            }

            _model.EncodeSymbol(_coder, EscapeSymbol);
            _coder.FlushEncode();
        }

        public void Decompress(Stream input, Stream output)
        {
            _coder.InitDecode(input);
            _model.StartDecoding();

            while (true)
            {
                int symbol = _model.DecodeSymbol(_coder);

                // 检查结束符号
                if (symbol == EscapeSymbol)
                    break;

                // 正常符号
                if (symbol >= 0)
                    output.WriteByte((byte)symbol);
                else
                    break;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _model?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }

    internal sealed class RangeCoder
    {
        private const uint TopValue = 0x01000000;
        private const uint BotValue = 0x00800000;
        private const uint RangeMax = 0xFFFFFFFF;

        private uint _low;
        private uint _range;
        private Stream _stream;
        private uint _cache;
        private int _cacheSize;
        private bool _encoding;

        public void InitEncode(Stream output)
        {
            _stream = output;
            _low = 0;
            _range = RangeMax;
            _cache = 0;
            _cacheSize = 1;
            _encoding = true;
        }

        public void InitDecode(Stream input)
        {
            _stream = input;
            _low = 0;
            _range = RangeMax;

            for (int i = 0; i < 4; i++)
            {
                _low = (_low << 8) | (byte)_stream.ReadByte();
            }
            _encoding = false;
        }

        public void Encode(uint start, uint size, uint total)
        {
            if (size == 0 || start >= total)
                throw new ArgumentException("Invalid range coding parameters");

            _range /= total;
            _low += start * _range;
            _range *= size;

            Normalize();
        }

        public uint GetFrequency(uint total)
        {
            return _range / total;
        }

        public void Update(uint start, uint size)
        {
            _low += start * _range;
            _range *= size;
            Normalize();
        }

        private void Normalize()
        {
            while (_range < BotValue)
            {
                if (_encoding)
                {
                    if (_low < 0xFF000000 || (_low >> 31) == 1)
                    {
                        ShiftLow();
                    }
                    else
                    {
                        _cacheSize++;
                    }
                }
                else
                {
                    _low = (_low << 8) | (byte)_stream.ReadByte();
                }
                _range <<= 8;
                _low <<= 8;
            }
        }

        private void ShiftLow()
        {
            uint lowHi = (_low >> 32);
            if (lowHi != 0 || _low < 0xFF000000)
            {
                uint temp = _cache;
                do
                {
                    _stream.WriteByte((byte)(temp + lowHi));
                    temp = 0xFF;
                } while (--_cacheSize != 0);
                _cache = (byte)(_low >> 24);
            }
            _cacheSize++;
            _low = (uint)((ulong)_low << 8);
        }

        public void FlushEncode()
        {
            for (int i = 0; i < 4; i++)
            {
                ShiftLow();
            }
        }
    }

    internal sealed class Model : IDisposable
    {
        private const int EscapeSymbol = 256;          // 逃逸符号
        private const int BinSymbolLimit = 257;        // 符号范围 (0-256)
        private const int InitialSum = BinSymbolLimit * 4; // 初始频率总和

        private class Context
        {
            public int[] Freqs { get; } = new int[BinSymbolLimit];
            public int Sum { get; set; } = InitialSum;
            public Context[] Successors { get; } = new Context[BinSymbolLimit];
            public int EscapeCount { get; set; }
            public int Order { get; }

            public Context(int order)
            {
                Order = order;
                // Initialize frequencies
                for (int i = 0; i < BinSymbolLimit; i++)
                {
                    Freqs[i] = 4; // Initial frequency count
                }
            }
        }

        private readonly int _maxOrder;
        private readonly int _memSize;
        private readonly int _compLevel;
        private Context _root;
        private Context _current;
        private int _contextCount;
        private int _maxContexts;
        private bool _disposed;

        public Model(int maxOrder, int memSize, int compLevel)
        {
            if (maxOrder < 2 || maxOrder > 16)
                throw new ArgumentException("Invalid model order");

            _maxOrder = maxOrder;
            _memSize = memSize;
            _compLevel = compLevel;

            // Calculate max contexts based on approx memory usage
            int contextSize = 256 * 4 + 256 * 8 + 32; // Approx size in bytes
            _maxContexts = Math.Max(1, _memSize / contextSize);

            InitializeModel();
        }

        private void InitializeModel()
        {
            _root = new Context(0);
            _current = _root;
            _contextCount = 1;
        }

        public void StartEncoding()
        {
            _current = _root;
        }

        public void StartDecoding()
        {
            _current = _root;
        }

        public void EncodeSymbol(RangeCoder coder, int symbol)
        {
            Context ctx = _current;
            int order = 0;

            while (ctx != null)
            {
                // 检查符号是否在有效范围内
                if (symbol < BinSymbolLimit)
                {
                    // 符号在当前上下文中存在
                    if (ctx.Freqs[symbol] > 0)
                    {
                        // 计算累积频率
                        uint cumFreq = 0;
                        for (int i = 0; i < symbol; i++)
                        {
                            cumFreq += (uint)ctx.Freqs[i];
                        }

                        // 编码符号
                        coder.Encode(cumFreq, (uint)ctx.Freqs[symbol], (uint)ctx.Sum);
                        UpdateModel(ctx, symbol);
                        return;
                    }

                    // 处理逃逸符号
                    uint escapeFreq = (uint)(ctx.EscapeCount + 1);
                    uint total = (uint)ctx.Sum + escapeFreq;
                    uint cumEscape = (uint)(ctx.Sum - ctx.Freqs[EscapeSymbol]);

                    // 编码逃逸符号
                    coder.Encode(cumEscape, escapeFreq, total);
                    ctx.EscapeCount++;

                    // 移动到低阶上下文
                    ctx = ctx.Successors[EscapeSymbol];
                    order++;
                }
                else
                {
                    // 无效符号，直接中断
                    break;
                }
            }

            // 零阶上下文处理 - 符号值在0-255之间
            if (symbol >= 0 && symbol < 256)
            {
                // 使用均匀分布直接编码字节
                coder.Encode((uint)symbol, 1, 256);
            }
            else
            {
                // 处理EscapeSymbol的特殊情况
                coder.Encode(255, 1, 256);
            }
        }

        public int DecodeSymbol(RangeCoder coder)
        {
            Context ctx = _current;
            int order = 0;

            while (ctx != null)
            {
                // 计算总频率（包括逃逸）
                uint total = (uint)ctx.Sum + (uint)ctx.EscapeCount + 1;
                uint freq = coder.GetFrequency(total);

                // 在符号范围内查找匹配
                uint cum = 0;
                int symbol;
                for (symbol = 0; symbol < BinSymbolLimit; symbol++)
                {
                    uint symbolFreq = (uint)ctx.Freqs[symbol];
                    if (freq < cum + symbolFreq) break;
                    cum += symbolFreq;
                }

                if (symbol < BinSymbolLimit)
                {
                    // 更新范围编码器
                    coder.Update(cum, (uint)ctx.Freqs[symbol]);

                    // 检查是否逃逸符号
                    if (symbol == EscapeSymbol)
                    {
                        ctx.EscapeCount++;
                        ctx = ctx.Successors[EscapeSymbol];
                        order++;
                        continue;
                    }

                    // 更新模型并返回解码的符号
                    UpdateModel(ctx, symbol);
                    return symbol;
                }

                // 处理逃逸情况
                uint escapeFreq = (uint)(ctx.EscapeCount + 1);
                coder.Update((uint)ctx.Sum, escapeFreq);
                ctx.EscapeCount++;
                ctx = ctx.Successors[EscapeSymbol];
                order++;
            }

            // 零阶上下文处理
            uint zeroFreq = coder.GetFrequency(256);
            int decodedSymbol = (int)zeroFreq;

            // 确保符号在0-255范围内
            if (decodedSymbol < 0) decodedSymbol = 0;
            if (decodedSymbol > 255) decodedSymbol = 255;

            coder.Update((uint)decodedSymbol, 1);
            return decodedSymbol;
        }

        public void EncodeEnd(RangeCoder coder)
        {
            // Encode termination symbol
            EncodeSymbol(coder, EscapeSymbol);
        }

        private void UpdateModel(Context ctx, int symbol)
        {
            // Update frequency counts
            ctx.Freqs[symbol]++;
            ctx.Sum++;

            // Periodically rescale frequencies
            if (ctx.Sum > 0x7FFF)
            {
                Model.RescaleFrequencies(ctx);
            }

            // Create new context if needed
            if (ctx.Successors[symbol] == null && ctx.Order < _maxOrder - 1)
            {
                if (_contextCount < _maxContexts)
                {
                    ctx.Successors[symbol] = new Context(ctx.Order + 1);
                    _contextCount++;
                }
            }

            // Move to new context
            _current = ctx.Successors[symbol] ?? _root;
        }

        private static void RescaleFrequencies(Context ctx)
        {
            int sum = 0;
            for (int i = 0; i < BinSymbolLimit; i++)
            {
                ctx.Freqs[i] = (ctx.Freqs[i] + 1) >> 1;
                sum += ctx.Freqs[i];
            }
            ctx.Sum = sum;
            ctx.EscapeCount = (ctx.EscapeCount + 1) >> 1;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Clean up context tree
                _root = null;
                _current = null;
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
#endif