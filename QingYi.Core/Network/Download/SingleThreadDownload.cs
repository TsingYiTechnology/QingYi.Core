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
        // 同步下载（基础版本）
        public static byte[] Download(string url, int bufferSize = 81920)
        {
            using var client = new HttpClient();
            using var response = client.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            return ProcessSyncStream(response.Content, bufferSize);
        }

        // 同步下载（支持自定义流处理）
        public static void Download(string url, Stream outputStream, int bufferSize = 81920)
        {
            using var client = new HttpClient();
            using var response = client.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            ProcessSyncStream(response.Content, outputStream, bufferSize);
        }

        // 异步下载（基础版本）
        public static async Task<byte[]> DownloadAsync(string url, int bufferSize = 81920, CancellationToken ct = default)
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return await ProcessAsyncStream(response.Content, bufferSize, ct).ConfigureAwait(false);
        }

        // 异步下载（支持自定义流处理）
        public static async Task DownloadAsync(string url, Stream outputStream, int bufferSize = 81920, CancellationToken ct = default)
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await ProcessAsyncStream(response.Content, outputStream, bufferSize, ct).ConfigureAwait(false);
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
