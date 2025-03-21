using System;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http.Headers;

namespace QingYi.Core.Network.Download
{
    public class MultiThreadDownload : IDisposable
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly Uri _downloadUrl;
        private readonly string _savePath;
        private readonly string _fileName;
        private readonly int _bufferSize;
        private long _totalSize;
        private long _downloadedBytes;
        private MemoryMappedFile _mmf;
        private bool _disposed;

        public MultiThreadDownload(Uri downloadUrl, string savePath, string fileName, int bufferSize = 81920)
        {
            _downloadUrl = downloadUrl ?? throw new ArgumentNullException(nameof(downloadUrl));
            _savePath = savePath ?? throw new ArgumentNullException(nameof(savePath));
            _fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            _bufferSize = bufferSize > 0 ? bufferSize : throw new ArgumentException("Buffer size must be positive");
        }

        public async Task StartDownloadAsync()
        {
            await GetFileSizeAsync();
            Directory.CreateDirectory(_savePath);
            var fullPath = Path.Combine(_savePath, _fileName);

            using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                fileStream.SetLength(_totalSize);
                _mmf = MemoryMappedFile.CreateFromFile(fileStream, null, _totalSize,
                    MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);

                try
                {
                    var threadCount = DetermineThreadCount();
                    var tasks = new Task[threadCount];

                    for (var i = 0; i < threadCount; i++)
                    {
                        var chunkSize = _totalSize / threadCount;
                        var start = i * chunkSize;
                        var end = (i == threadCount - 1) ? _totalSize - 1 : start + chunkSize - 1;
                        tasks[i] = DownloadChunkAsync(start, end);
                    }

                    await Task.WhenAll(tasks);
                }
                finally
                {
                    _mmf?.Dispose();
                }
            }
        }

        private async Task GetFileSizeAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, _downloadUrl);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();
            _totalSize = response.Content.Headers.ContentLength ?? throw new InvalidOperationException("Missing Content-Length");

            if (!response.Headers.AcceptRanges.Contains("bytes"))
                throw new NotSupportedException("Server does not support range requests");
        }

        private int DetermineThreadCount()
        {
            const long minChunkSize = 4 * 1024 * 1024; // 4MB per chunk
            var maxThreads = Environment.ProcessorCount * 2;
            var threadCount = (int)Math.Min(_totalSize / minChunkSize, maxThreads);
            return Math.Max(1, threadCount);
        }

        private async Task DownloadChunkAsync(long start, long end)
        {
            var chunkSize = end - start + 1;
            using var accessor = _mmf.CreateViewAccessor(start, chunkSize, MemoryMappedFileAccess.Write);
            var buffer = new byte[_bufferSize];

            unsafe
            {
                byte* destPtr = (byte*)accessor.SafeMemoryMappedViewHandle.DangerousGetHandle();
                long currentOffset = 0;

                using var request = new HttpRequestMessage(HttpMethod.Get, _downloadUrl);
                request.Headers.Range = new RangeHeaderValue(start, end);

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();

                while (currentOffset < chunkSize)
                {
                    var bytesToRead = (int)Math.Min(buffer.Length, chunkSize - currentOffset);
                    var bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead);

                    if (bytesRead == 0)
                        throw new EndOfStreamException("Unexpected end of stream");

                    fixed (byte* srcPtr = buffer)
                    {
                        Buffer.MemoryCopy(srcPtr, destPtr + currentOffset, bytesRead, bytesRead);
                    }

                    currentOffset += bytesRead;
                    Interlocked.Add(ref _downloadedBytes, bytesRead);
                }
            }
        }

        public double GetProgress()
        {
            var downloaded = Interlocked.Read(ref _downloadedBytes);
            return (double)downloaded / _totalSize;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _mmf?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
