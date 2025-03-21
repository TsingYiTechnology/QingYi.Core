using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace QingYi.Core.Network.Download
{
    /// <summary>
    /// Single threaded download class.<br />
    /// 单线程下载类。
    /// </summary>
    public class SingleThreadDownload
    {
        public delegate void DownloadProgressHandler(long downloadedBytes, long? totalBytes);

        // 同步保存到文件的重载
        public static void Download(string url, string savePath, string fileName, int bufferSize = 81920, DownloadProgressHandler progressHandler = null)
        {
            var fullPath = Path.Combine(savePath, fileName);
            Directory.CreateDirectory(savePath);

            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            Download(url, fileStream, bufferSize, progressHandler);
        }

        // 仅适用于下载到当前目录
        public static void Download(string url, string fullPath, int bufferSize = 81920, DownloadProgressHandler progressHandler = null)
        {
            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            Download(url, fileStream, bufferSize, progressHandler);
        }

        // 扩展原有同步方法支持进度
        public static void Download(string url, Stream outputStream, int bufferSize = 81920, DownloadProgressHandler progressHandler = null)
        {
            using var client = new HttpClient();
            using var response = client.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            ReadStreamWithProgress(
                response.Content.ReadAsStreamAsync().GetAwaiter().GetResult(),
                outputStream,
                response.Content.Headers.ContentLength,
                bufferSize,
                progressHandler);
        }

        // 异步保存到文件的重载
        public static async Task DownloadAsync(string url, string savePath, string fileName, int bufferSize = 81920, DownloadProgressHandler progressHandler = null, CancellationToken ct = default)
        {
            var fullPath = Path.Combine(savePath, fileName);
            Directory.CreateDirectory(savePath);

            await using var fileStream = new FileStream(
                fullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            await DownloadAsync(url, fileStream, bufferSize, progressHandler, ct).ConfigureAwait(false);
        }

        // 仅适用于下载到当前目录
        public static async Task DownloadAsync(string url, string fullPath, int bufferSize = 81920, DownloadProgressHandler progressHandler = null, CancellationToken ct = default)
        {
            await using var fileStream = new FileStream(
                fullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            await DownloadAsync(url, fileStream, bufferSize, progressHandler, ct).ConfigureAwait(false);
        }

        // 扩展原有异步方法支持进度
        public static async Task DownloadAsync(string url, Stream outputStream, int bufferSize = 81920, DownloadProgressHandler progressHandler = null, CancellationToken ct = default)
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await ReadStreamWithProgressAsync(
                await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
                outputStream,
                response.Content.Headers.ContentLength,
                bufferSize,
                progressHandler,
                ct).ConfigureAwait(false);
        }

        private static byte[] ProcessSyncStream(HttpContent content, int bufferSize)
        {
            var contentLength = content.Headers.ContentLength;
            using var stream = content.ReadAsStreamAsync().GetAwaiter().GetResult();

            if (contentLength.HasValue)
            {
                return ReadStreamWithKnownLength(stream, contentLength.Value, bufferSize);
            }

            return ReadStreamWithUnknownLength(stream, bufferSize);
        }

        private static void ProcessSyncStream(HttpContent content, Stream outputStream, int bufferSize)
        {
            using var stream = content.ReadAsStreamAsync().GetAwaiter().GetResult();
            ReadStreamToOutput(stream, outputStream, bufferSize);
        }

        private static async Task<byte[]> ProcessAsyncStream(HttpContent content, int bufferSize, CancellationToken ct)
        {
            var contentLength = content.Headers.ContentLength;
            using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);

            if (contentLength.HasValue)
            {
                return await ReadStreamWithKnownLengthAsync(stream, contentLength.Value, bufferSize, ct)
                    .ConfigureAwait(false);
            }

            return await ReadStreamWithUnknownLengthAsync(stream, bufferSize, ct).ConfigureAwait(false);
        }

        private static async Task ProcessAsyncStream(HttpContent content, Stream outputStream, int bufferSize, CancellationToken ct)
        {
            using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
            await ReadStreamToOutputAsync(stream, outputStream, bufferSize, ct).ConfigureAwait(false);
        }

        #region Progress-Enhanced Implementations
        private static unsafe void ReadStreamWithProgress(
            Stream input,
            Stream output,
            long? totalSize,
            int bufferSize,
            DownloadProgressHandler progressHandler)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            long totalRead = 0;
            int reportThreshold = CalculateReportThreshold(totalSize);
            int nextReport = reportThreshold;

            try
            {
                int bytesRead;
                while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fixed (byte* bufferPtr = buffer)
                    {
                        output.Write(new ReadOnlySpan<byte>(bufferPtr, bytesRead));
                    }

                    totalRead += bytesRead;
                    if (totalRead >= nextReport || totalRead == totalSize)
                    {
                        progressHandler?.Invoke(totalRead, totalSize);
                        nextReport += reportThreshold;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static async Task ReadStreamWithProgressAsync(
            Stream input,
            Stream output,
            long? totalSize,
            int bufferSize,
            DownloadProgressHandler progressHandler,
            CancellationToken ct)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            long totalRead = 0;
            int reportThreshold = CalculateReportThreshold(totalSize);
            int nextReport = reportThreshold;

            try
            {
                int bytesRead;
                while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                    totalRead += bytesRead;

                    if (totalRead >= nextReport || totalRead == totalSize)
                    {
                        progressHandler?.Invoke(totalRead, totalSize);
                        nextReport += reportThreshold;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static int CalculateReportThreshold(long? totalSize)
        {
            if (!totalSize.HasValue) return 1024 * 1024; // 1MB 报告间隔
            if (totalSize < 10 * 1024 * 1024) return (int)(totalSize / 100); // 小文件按百分比
            return (int)(totalSize / 100); // 大文件至少1%间隔
        }
        #endregion

        #region Sync Implementations
        private static unsafe byte[] ReadStreamWithKnownLength(Stream stream, long contentLength, int bufferSize)
        {
            byte[] result = new byte[contentLength];
            int bytesRead;
            int totalRead = 0;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Min(bufferSize, result.Length));
            try
            {
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fixed (byte* src = buffer)
                    fixed (byte* dest = result)
                    {
                        Buffer.MemoryCopy(src, dest + totalRead, bytesRead, bytesRead);
                    }
                    totalRead += bytesRead;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return result;
        }

        private static byte[] ReadStreamWithUnknownLength(Stream stream, int bufferSize)
        {
            using var ms = new MemoryStream();
            ReadStreamToOutput(stream, ms, bufferSize);
            return ms.ToArray();
        }

        private static unsafe void ReadStreamToOutput(Stream input, Stream output, int bufferSize)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                int bytesRead;
                while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fixed (byte* bufferPtr = buffer)
                    {
                        Span<byte> span = new Span<byte>(bufferPtr, bytesRead);
                        output.Write(span);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        #endregion

        #region Async Implementations
        private static async Task<byte[]> ReadStreamWithKnownLengthAsync(Stream stream, long contentLength, int bufferSize, CancellationToken ct)
        {
            byte[] result = new byte[contentLength];
            int totalRead = 0;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Min(bufferSize, result.Length));
            try
            {
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    await UnsafeMemoryCopyAsync(buffer, bytesRead, result, totalRead).ConfigureAwait(false);
                    totalRead += bytesRead;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return result;
        }

        private static async Task<byte[]> ReadStreamWithUnknownLengthAsync(Stream stream, int bufferSize, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await ReadStreamToOutputAsync(stream, ms, bufferSize, ct).ConfigureAwait(false);
            return ms.ToArray();
        }

        private static async Task ReadStreamToOutputAsync(Stream input, Stream output, int bufferSize, CancellationToken ct)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                int bytesRead;
                while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        #endregion

        #region Unsafe Codes
        private static unsafe Task UnsafeMemoryCopyAsync(byte[] source, int sourceLength, byte[] destination, int destOffset)
        {
            fixed (byte* src = source)
            fixed (byte* dest = destination)
            {
                Buffer.MemoryCopy(src, dest + destOffset, sourceLength, sourceLength);
            }
            return Task.CompletedTask;
        }
        #endregion
    }
}
