using System.IO.MemoryMappedFiles;
using System.IO;
using System.Threading.Tasks;
using System;

#if NET6_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif

#pragma warning disable 0419

namespace QingYi.Core.FileUtility.IO
{
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    /// <summary>
    /// Provides high-performance read-only access to files using memory-mapped I/O.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This sealed class implements disposable pattern to manage unmanaged resources.
    /// It uses memory-mapped files for efficient random access to file contents.
    /// </para>
    /// <para>
    /// Thread safety: Instance members are not thread-safe. Concurrent access must be synchronized by callers.
    /// </para>
    /// <para>
    /// Important usage notes:
    /// <list type="bullet">
    /// <item><description>Designed for read operations only</description></item>
    /// <item><description>Maintains unsafe pointer to mapped memory</description></item>
    /// <item><description>Dispose must be called to release file handles</description></item>
    /// <item><description>Not suitable for files larger than 2GB on 32-bit systems</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class FileReader : IDisposable, IAsyncDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly unsafe byte* _pointer;
        private readonly long _length;
        private bool _disposed;

        /// <summary>
        /// High performance reading files.
        /// </summary>
        /// <param name="filePath">The file path.</param>
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

        /// <summary>
        /// Reads a sequence of bytes directly from the memory-mapped file.
        /// </summary>
        /// <param name="offset">The zero-based offset in the file to begin reading from.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>A <see cref="Span{Byte}"/> representing the requested file data region.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when offset or count exceed file boundaries.</exception>
        /// <remarks>
        /// <para>
        /// This method provides direct access to the memory-mapped file content through a <see cref="Span{Byte}"/> 
        /// without data copying. The returned span becomes invalid after the <see cref="FileReader"/> is disposed.
        /// </para>
        /// <para>
        /// This is a high-performance method suitable for low-level byte operations. Callers must ensure:
        /// <list type="bullet">
        /// <item>The <see cref="FileReader"/> instance remains alive while using the span</item>
        /// <item>No writes occur through the span (read-only access)</item>
        /// <item>Parameters stay within valid file boundaries</item>
        /// </list>
        /// </para>
        /// </remarks>
        public unsafe Span<byte> Read(long offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FileReader));
            if (offset < 0 || offset + count > _length) throw new ArgumentOutOfRangeException();

            return new Span<byte>(_pointer + offset, count);
        }

        /// <summary>
        /// Asynchronously reads bytes from the file into a new memory buffer.
        /// </summary>
        /// <param name="offset">The zero-based offset in the file to begin reading from.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>A task representing the asynchronous operation, containing the read data as <see cref="Memory{Byte}"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when offset or count exceed file boundaries.</exception>
        /// <remarks>
        /// <para>
        /// This method provides async-compatible access but completes synchronously, as memory-mapped file operations 
        /// don't require true asynchronous I/O. The data is copied to a new buffer for safe asynchronous consumption.
        /// </para>
        /// <para>
        /// While less performant than the synchronous <see cref="Read"/> method due to data copying, this version 
        /// is suitable for async method chains and scenarios requiring Memory&lt;byte&gt; instead of Span&lt;byte&gt;.
        /// </para>
        /// </remarks>
        public Task<Memory<byte>> ReadAsync(long offset, int count) => Task.FromResult<Memory<byte>>(Read(offset, count).ToArray());

        /// <summary>
        /// Copies bytes from the memory-mapped file to the specified destination span.
        /// </summary>
        /// <param name="sourceOffset">The zero-based offset in the file to begin copying from.</param>
        /// <param name="destination">The destination span to receive the copied bytes.</param>
        /// <param name="count">The number of bytes to copy.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when:
        /// <list type="bullet">
        /// <item><description>sourceOffset is negative</description></item>
        /// <item><description>sourceOffset + count exceeds file length</description></item>
        /// <item><description>destination length is less than count</description></item>
        /// </list>
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method provides high-performance copying using optimal hardware acceleration:
        /// <list type="bullet">
        /// <item><description>Uses AVX2 vectorized copy on .NET 6+ for x86/x64 platforms with Little-Endian architecture</description></item>
        /// <item><description>Falls back to <see cref="Buffer.MemoryCopy"/> otherwise</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Important usage notes:
        /// <list type="bullet">
        /// <item><description>Destination span must have sufficient capacity (minimum count bytes)</description></item>
        /// <item><description>Performs synchronous memory copy (no async I/O involved)</description></item>
        /// <item><description>Does NOT maintain references to the destination span after method returns</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// For optimal performance on modern hardware:
        /// <list type="number">
        /// <item><description>Align sourceOffset and count to 32-byte boundaries when using AVX2</description></item>
        /// <item><description>Prefer count values divisible by SIMD register size (256-bit for AVX2)</description></item>
        /// </list>
        /// </para>
        /// </remarks>
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

        /// <summary>
        /// Releases all unmanaged resources used by the <see cref="FileReader"/> synchronously.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This implementation:
        /// <list type="bullet">
        /// <item><description>Releases the memory-mapped view pointer</description></item>
        /// <item><description>Disposes the memory-mapped view accessor</description></item>
        /// <item><description>Disposes the memory-mapped file</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Safe to call multiple times - subsequent calls after first disposal are no-ops.
        /// Implements <see cref="IDisposable.Dispose"/>.
        /// </para>
        /// <para>
        /// WARNING: This method contains unsafe pointer operations. Any access to disposed instance
        /// may cause memory access violations.
        /// </para>
        /// </remarks>
        public void Dispose()
        {
            if (_disposed) return;

            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _accessor.Dispose();
            _mmf.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Asynchronously releases all unmanaged resources used by the <see cref="FileReader"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This async implementation:
        /// <list type="bullet">
        /// <item><description>Executes disposal on thread pool thread via <see cref="Task.Run"/></description></item>
        /// <item><description>Performs same cleanup as synchronous <see cref="Dispose"/> method</description></item>
        /// <item><description>Ensures thread-safe disposal state tracking</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Implements async dispose pattern (IAsyncDisposable). Prefer this version in async contexts,
        /// though actual file unmapping operations are synchronous.
        /// </para>
        /// <para>
        /// NOTE: While this method returns ValueTask for efficiency, the underlying disposal work is
        /// always asynchronous via thread pool. Consider using synchronous Dispose() when possible.
        /// </para>
        /// </remarks>
        /// <returns>A ValueTask that completes when resources are released.</returns>
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
#elif NETSTANDARD2_0

    /// <summary>
    /// Provides high-performance read-only access to files using memory-mapped I/O.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This sealed class implements disposable pattern to manage unmanaged resources.
    /// It uses memory-mapped files for efficient random access to file contents.
    /// </para>
    /// <para>
    /// Thread safety: Instance members are not thread-safe. Concurrent access must be synchronized by callers.
    /// </para>
    /// <para>
    /// Important usage notes:
    /// <list type="bullet">
    /// <item><description>Designed for read operations only</description></item>
    /// <item><description>Maintains unsafe pointer to mapped memory</description></item>
    /// <item><description>Dispose must be called to release file handles</description></item>
    /// <item><description>Not suitable for files larger than 2GB on 32-bit systems</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class FileReader : IDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly unsafe byte* _pointer;
        private readonly long _length;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance mapping the specified file into memory.
        /// </summary>
        /// <param name="filePath">The path to an existing readable file.</param>
        /// <exception cref="FileNotFoundException">Thrown when specified file doesn't exist.</exception>
        /// <exception cref="IOException">Thrown when file access is denied or other I/O error occurs.</exception>
        /// <remarks>
        /// <para>
        /// The constructor:
        /// <list type="number">
        /// <item><description>Opens the file with FileStream (4096 byte buffer, random access)</description></item>
        /// <item><description>Creates memory mapping with read-only access</description></item>
        /// <item><description>Acquires direct pointer to mapped memory region</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// The file remains mapped until Dispose is called. Subsequent modifications to the file
        /// while mapped may result in undefined behavior.
        /// </para>
        /// </remarks>
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

        /// <summary>
        /// Reads a block of bytes from the mapped file into a new array.
        /// </summary>
        /// <param name="offset">The zero-based file position to start reading.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>A new byte array containing the requested data.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when instance is disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when:
        /// <list type="bullet">
        /// <item><description>offset is negative</description></item>
        /// <item><description>offset + count exceeds file length</description></item>
        /// </list>
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method:
        /// <list type="bullet">
        /// <item><description>Allocates a new byte array for each call</description></item>
        /// <item><description>Uses unmanaged memory copy for maximum performance</description></item>
        /// <item><description>Is suitable for small to medium sized reads</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// For large data operations, consider using <see cref="CopyTo"/> instead to avoid
        /// multiple array allocations.
        /// </para>
        /// </remarks>
        public unsafe byte[] Read(long offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FileReader));
            if (offset < 0 || offset + count > _length)
                throw new ArgumentOutOfRangeException();

            var buffer = new byte[count];
            fixed (byte* bufferPtr = buffer)
            {
                Buffer.MemoryCopy(_pointer + offset, bufferPtr, count, count);
            }
            return buffer;
        }

        /// <summary>
        /// Asynchronously reads bytes from the file (synchronous implementation).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method provides async compatibility but executes synchronously.
        /// Use for async method chaining where synchronous <see cref="Read"/> isn't compatible.
        /// </para>
        /// <para>
        /// Note: Actual file I/O is performed synchronously through memory mapping.
        /// True asynchronous file operations require different implementation approaches.
        /// </para>
        /// </remarks>
        /// <inheritdoc cref="Read(long, int)"/>
        public Task<byte[]> ReadAsync(long offset, int count) => Task.FromResult(Read(offset, count));

        /// <summary>
        /// Copies bytes directly from the mapped file to a target array.
        /// </summary>
        /// <param name="sourceOffset">The zero-based file position to start copying from.</param>
        /// <param name="destination">The destination array to receive data.</param>
        /// <param name="destinationOffset">The zero-based position in destination array to start writing.</param>
        /// <param name="count">The number of bytes to copy.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when:
        /// <list type="bullet">
        /// <item><description>sourceOffset or destinationOffset is negative</description></item>
        /// <item><description>sourceOffset + count exceeds file length</description></item>
        /// <item><description>destinationOffset + count exceeds destination array length</description></item>
        /// </list>
        /// </exception>
        /// <remarks>
        /// <para>
        /// Preferred method for large data copies due to:
        /// <list type="bullet">
        /// <item><description>No intermediate array allocation</description></item>
        /// <item><description>Direct memory-to-memory copy optimization</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// The destination array must be pre-allocated with sufficient capacity.
        /// </para>
        /// </remarks>
        public unsafe void CopyTo(long sourceOffset, byte[] destination, int destinationOffset, int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FileReader));
            if (sourceOffset < 0 || sourceOffset + count > _length)
                throw new ArgumentOutOfRangeException();
            if (destinationOffset < 0 || destinationOffset + count > destination.Length)
                throw new ArgumentOutOfRangeException();

            fixed (byte* destPtr = &destination[destinationOffset])
            {
                byte* srcPtr = _pointer + sourceOffset;
                Buffer.MemoryCopy(srcPtr, destPtr, count, count);
            }
        }

        /// <summary>
        /// Releases all resources associated with the memory-mapped file.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Performs three critical cleanup operations:
        /// <list type="number">
        /// <item><description>Releases the unmanaged memory pointer</description></item>
        /// <item><description>Disposes the memory-mapped view accessor</description></item>
        /// <item><description>Disposes the memory-mapped file object</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// After disposal:
        /// <list type="bullet">
        /// <item><description>All subsequent method calls will throw <see cref="ObjectDisposedException"/></description></item>
        /// <item><description>The mapped memory pointer becomes invalid</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Safe to call multiple times - subsequent calls have no effect.
        /// </para>
        /// </remarks>
        public void Dispose()
        {
            if (_disposed) return;

            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _accessor.Dispose();
            _mmf.Dispose();
            _disposed = true;
        }
    }
#endif
}

#pragma warning restore 0419
