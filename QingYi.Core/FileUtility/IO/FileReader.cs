using System.IO.MemoryMappedFiles;
using System.IO;
using System.Threading.Tasks;
using System;

#if NET6_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif

namespace QingYi.Core.FileUtility.IO
{
    public sealed class FileReader : IDisposable, IAsyncDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly unsafe byte* _pointer;
        private readonly long _length;
        private bool _disposed;

        public unsafe FileReader(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            _length = fileInfo.Length;

            var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.RandomAccess);

            _mmf = MemoryMappedFile.CreateFromFile(
                fileStream,
                null,
                _length,
                MemoryMappedFileAccess.Read,
                HandleInheritability.None,
                leaveOpen: false);

            _accessor = _mmf.CreateViewAccessor(0, _length, MemoryMappedFileAccess.Read);

            byte* ptr = null;
            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            _pointer = ptr;
        }

        public unsafe Span<byte> Read(long offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FileReader));
            if (offset < 0 || offset + count > _length) throw new ArgumentOutOfRangeException();

            return new Span<byte>(_pointer + offset, count);
        }

        public Task<Memory<byte>> ReadAsync(long offset, int count)
        {
            // 对于内存映射文件，异步操作实际上同步完成
            return Task.FromResult<Memory<byte>>(Read(offset, count).ToArray());
        }

        public unsafe void CopyTo(long sourceOffset, Span<byte> destination, int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FileReader));
            if (sourceOffset < 0 || sourceOffset + count > _length)
                throw new ArgumentOutOfRangeException();

            fixed (byte* destPtr = destination)
            {
                byte* srcPtr = _pointer + sourceOffset;

#if NET6_0_OR_GREATER
                if (Avx2.IsSupported && BitConverter.IsLittleEndian)
                {
                    VectorizedCopy(destPtr, srcPtr, count);
                    return;
                }
#endif
                Buffer.MemoryCopy(srcPtr, destPtr, count, count);
            }
        }

#if NET6_0_OR_GREATER
        private unsafe void VectorizedCopy(byte* dest, byte* src, int count)
        {
            const int vectorSize = 256 / 8; // AVX2向量大小
            int i = 0;

            // 使用AVX2进行向量化复制
            for (; i <= count - vectorSize; i += vectorSize)
            {
                var vector = Avx.LoadVector256(src + i);
                Avx.Store(dest + i, vector);
            }

            // 处理剩余字节
            for (; i < count; i++)
            {
                dest[i] = src[i];
            }
        }
#endif

        public void Dispose()
        {
            if (_disposed) return;

            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _accessor.Dispose();
            _mmf.Dispose();
            _disposed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            await Task.Run(() =>
            {
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                _accessor.Dispose();
                _mmf.Dispose();
            });
            _disposed = true;
        }
    }
}
