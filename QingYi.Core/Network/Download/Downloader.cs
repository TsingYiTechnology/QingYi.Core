using System;

namespace QingYi.Core.Network.Download
{
    public class Downloader
    {
        public static void Download(string url, string savePath, string fileName, DownloadType downloadType = DownloadType.SingleThread) => DownloadCommon(url, savePath, fileName, 81920, downloadType);

        public static void Download(string url, string savePath, string fileName, int bufferSize = 81920, DownloadType downloadType = DownloadType.SingleThread) => DownloadCommon(url, savePath, fileName, bufferSize, downloadType);

        private static void DownloadCommon(string url, string savePath, string fileName, int bufferSize, DownloadType downloadType = DownloadType.SingleThread)
        {
            if (downloadType == DownloadType.SingleThread)
            {
                SingleThreadDownload.Download(url, savePath, fileName, bufferSize);
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

        MultiThread,
    }
}
