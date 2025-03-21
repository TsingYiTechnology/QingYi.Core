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
            // 第一阶段：尝试使用HEAD方法获取
            try
            {
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, _downloadUrl);
                using var headResponse = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead);

                if (ValidateResponse(headResponse))
                {
                    _totalSize = headResponse.Content.Headers.ContentLength ?? -1;
                    if (_totalSize > 0) return;
                }
            }
            catch (HttpRequestException) { /* 忽略HEAD请求失败，继续尝试GET */ }

            // 第二阶段：使用带Range请求的GET方法验证
            try
            {
                using var partialRequest = new HttpRequestMessage(HttpMethod.Get, _downloadUrl);
                partialRequest.Headers.Range = new RangeHeaderValue(0, 0);

                using var partialResponse = await _httpClient.SendAsync(partialRequest, HttpCompletionOption.ResponseHeadersRead);

                if (ValidateResponse(partialResponse))
                {
                    // 通过Content-Range解析总大小
                    var contentRange = partialResponse.Content.Headers.ContentRange;
                    if (contentRange != null && contentRange.HasLength)
                    {
                        _totalSize = (long)contentRange.Length; // 暂时不会坏
                        return;
                    }
                }
            }
            catch (HttpRequestException) { /* 忽略部分请求失败 */ }

            // 第三阶段：完整请求获取（最后手段）
            using var fullRequest = new HttpRequestMessage(HttpMethod.Get, _downloadUrl);
            using var fullResponse = await _httpClient.SendAsync(fullRequest, HttpCompletionOption.ResponseHeadersRead);

            if (ValidateResponse(fullResponse))
            {
                _totalSize = fullResponse.Content.Headers.ContentLength ?? -1;
                if (_totalSize > 0) return;
            }

            throw new NotSupportedException("The file size cannot be determined and the server does not support scope requests."); // 无法确定文件大小且服务器不支持范围请求
        }

        private bool ValidateResponse(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();

            // 检查范围请求支持
            if (!response.Headers.AcceptRanges.Contains("bytes"))
                throw new NotSupportedException("The server does not support byte range requests."); // 服务器不支持字节范围请求

            // 验证内容是否可寻址
            if (response.Headers.AcceptRanges.Count == 0 &&
                response.Content.Headers.ContentRange == null)
                return false;

            return true;
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

            using var request = new HttpRequestMessage(HttpMethod.Get, _downloadUrl);
            request.Headers.Range = new RangeHeaderValue(start, end);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();

            long currentOffset = 0;
            while (currentOffset < chunkSize)
            {
                var bytesToRead = (int)Math.Min(buffer.Length, chunkSize - currentOffset);
                var bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead);

                if (bytesRead == 0)
                    throw new EndOfStreamException("Unexpected end of stream");

                unsafe
                {
                    accessor.WriteArray(currentOffset, buffer, 0, bytesRead);

                    // 或者使用更底层的指针操作（可选）
                    /*
                    fixed (byte* srcPtr = buffer)
                    {
                        byte* destPtr = (byte*)accessor.SafeMemoryMappedViewHandle.DangerousGetHandle() + currentOffset;
                        Buffer.MemoryCopy(srcPtr, destPtr, bytesRead, bytesRead);
                    }
                    */
                }

                currentOffset += bytesRead;
                Interlocked.Add(ref _downloadedBytes, bytesRead);
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
