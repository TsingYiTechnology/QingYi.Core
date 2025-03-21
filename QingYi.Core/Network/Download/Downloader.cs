using System;
using System.Threading.Tasks;

namespace QingYi.Core.Network.Download
{
    public class Downloader
    {
        public static void Download(string url, string savePath, string fileName, DownloadType downloadType = DownloadType.SingleThread) => DownloadCommon(url, savePath, fileName, 81920, downloadType);

        public static void Download(string url, string savePath, string fileName, int bufferSize = 81920, DownloadType downloadType = DownloadType.SingleThread) => DownloadCommon(url, savePath, fileName, bufferSize, downloadType);

        public static async Task DownloadAsync(string url, string savePath, string fileName, DownloadType downloadType = DownloadType.SingleThread) => await DownloadCommonAsync(url, savePath, fileName, 81920, downloadType);

        public static async Task DownloadAsync(string url, string savePath, string fileName, int bufferSize = 81920, DownloadType downloadType = DownloadType.SingleThread) => await DownloadCommonAsync(url, savePath, fileName, bufferSize, downloadType);

        private static void DownloadCommon(string url, string savePath, string fileName, int bufferSize, DownloadType downloadType = DownloadType.SingleThread)
        {
            if (downloadType == DownloadType.SingleThread)
            {
                SingleThreadDownload.Download(url, savePath, fileName, bufferSize);
            }
            else if (downloadType == DownloadType.MultiThread)
            {
                throw new ArgumentException("Multithreaded downloads support asynchronous only.");
            }
        }

        private static async Task DownloadCommonAsync(string url, string savePath, string fileName, int bufferSize, DownloadType downloadType = DownloadType.SingleThread)
        {
            if (downloadType == DownloadType.SingleThread)
            {
                await SingleThreadDownload.DownloadAsync(url, savePath, fileName, bufferSize);
            }
            else if (downloadType == DownloadType.MultiThread)
            {
                var downloader = new MultiThreadDownload(new Uri(url), savePath, fileName, bufferSize);

                await downloader.StartDownloadAsync();
            }
        }
    }

    [Flags]
    public enum DownloadType
    {
        /// <summary>
        /// Use <see cref="SingleThreadDownload"/> class.
        /// </summary>
        SingleThread,

        /// <summary>
        /// Use <see cref="MultiThreadDownload"/> class.
        /// </summary>
        MultiThread,
    }
}
