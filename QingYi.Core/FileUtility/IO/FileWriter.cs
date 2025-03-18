using System;
using System.IO;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Threading;

namespace QingYi.Core.FileUtility.IO
{
    public sealed class FileWriter : IDisposable, IAsyncDisposable
    {
        private readonly FileStream _fileStream;
        private readonly byte[] _buffer;
        private int _bufferPosition;
        private readonly int _bufferSize;
        private readonly bool _leaveOpen;

        // IL优化的内存拷贝方法
        private static readonly MemoryCopier _memoryCopier = new MemoryCopier();

        public FileWriter(string path,
                         int bufferSize = 81920,
                         FileMode mode = FileMode.Create,
                         FileAccess access = FileAccess.Write,
                         FileShare share = FileShare.Read,
                         FileOptions options = FileOptions.None)
            : this(new FileStream(path, mode, access, share, bufferSize, options), bufferSize, false)
        {
        }

        public FileWriter(FileStream stream, int bufferSize = 81920, bool leaveOpen = false)
        {
            _fileStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _bufferSize = bufferSize;
            _buffer = new byte[bufferSize];
            _bufferPosition = 0;
            _leaveOpen = leaveOpen;
        }

        // 同步写入方法
        public void Write(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty) return;

            int remaining = data.Length;
            int offset = 0;

            while (remaining > 0)
            {
                int available = _bufferSize - _bufferPosition;
                if (available == 0)
                {
                    Flush();
                    available = _bufferSize;
                }

                int copyBytes = Math.Min(available, remaining);

                unsafe
                {
                    fixed (byte* src = &data[offset])
                    fixed (byte* dest = &_buffer[_bufferPosition])
                    {
                        // 参数顺序修正为：目标地址，源地址
                        _memoryCopier.Copy((IntPtr)dest, (IntPtr)src, copyBytes);
                    }
                }

                _bufferPosition += copyBytes;
                offset += copyBytes;
                remaining -= copyBytes;

                // 直接处理超大块数据
                if (remaining > _bufferSize)
                {
                    Flush();
                    WriteDirect(data.Slice(offset, remaining));
                    return;
                }
            }
        }

        // 异步写入方法
        public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            if (data.IsEmpty) return;

            int remaining = data.Length;
            int offset = 0;

            while (remaining > 0)
            {
                int available = _bufferSize - _bufferPosition;
                if (available == 0)
                {
                    await FlushAsync(cancellationToken).ConfigureAwait(false);
                    available = _bufferSize;
                }

                int copyBytes = Math.Min(available, remaining);

                // 拷贝到缓冲区
                unsafe
                {
                    fixed (byte* src = &data.Span[offset])
                    fixed (byte* dest = &_buffer[_bufferPosition])
                    {
                        _memoryCopier.Copy((IntPtr)dest, (IntPtr)src, copyBytes);
                    }
                }

                _bufferPosition += copyBytes;
                offset += copyBytes;
                remaining -= copyBytes;

                // 关键修复：动态处理剩余数据
                if (remaining > 0)
                {
                    // 当剩余数据超过缓冲区容量时
                    if (remaining > _bufferSize)
                    {
                        await FlushAsync(cancellationToken).ConfigureAwait(false);
                        await WriteDirectAsync(
                            data.Slice(offset, remaining),
                            cancellationToken
                        ).ConfigureAwait(false);

                        offset += remaining; // 更新偏移量
                        remaining = 0;      // 终止循环
                    }
                }
            }
        }

        // 改进的直接写入方法（处理部分写入）
        private async ValueTask WriteDirectAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            await _fileStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }

        private void WriteDirect(ReadOnlySpan<byte> data)
        {
            unsafe
            {
                fixed (byte* ptr = data)
                {
                    using var ums = new UnmanagedMemoryStream(ptr, data.Length);
                    ums.CopyTo(_fileStream);
                }
            }
        }

        public void Flush()
        {
            if (_bufferPosition > 0)
            {
                _fileStream.Write(_buffer, 0, _bufferPosition);
                _bufferPosition = 0;
            }
        }

        public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            if (_bufferPosition > 0)
            {
                await _fileStream.WriteAsync(_buffer, 0, _bufferPosition, cancellationToken)
                    .ConfigureAwait(false);
                _bufferPosition = 0;
            }
        }

        public void Dispose()
        {
            Flush();
            if (!_leaveOpen)
            {
                _fileStream.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await FlushAsync().ConfigureAwait(false);
            if (!_leaveOpen)
            {
                await _fileStream.DisposeAsync().ConfigureAwait(false);
            }
        }

        // IL优化的内存拷贝实现
        private sealed class MemoryCopier
        {
            internal delegate void CopyMethod(IntPtr dest, IntPtr src, int count);
            public readonly CopyMethod Copy;

            public MemoryCopier()
            {
                var dynamicMethod = new DynamicMethod(
                    "ILMemCopy",
                    null,
                    new[] { typeof(IntPtr), typeof(IntPtr), typeof(int) },
                    typeof(MemoryCopier).Module,
                    true);

                var il = dynamicMethod.GetILGenerator();
                // 修正参数顺序：目标 -> 源 -> 长度
                il.Emit(OpCodes.Ldarg_0); // 目标地址
                il.Emit(OpCodes.Ldarg_1); // 源地址
                il.Emit(OpCodes.Ldarg_2); // 长度
                il.Emit(OpCodes.Cpblk);
                il.Emit(OpCodes.Ret);

                Copy = (CopyMethod)dynamicMethod.CreateDelegate(typeof(CopyMethod));
            }
        }
    }
}
