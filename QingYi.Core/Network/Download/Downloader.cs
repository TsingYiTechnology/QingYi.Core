using System;
using System.Threading.Tasks;

namespace QingYi.Core.Network.Download
{
    /// <summary>
    /// Easier to download class calls.<br />
    /// 更简易的下载类调用。
    /// </summary>
    public class Downloader
    {
        /// <summary>
        /// Downloads a file from the specified URL and saves it to the given path with the specified file name. 
        /// This method uses a single-threaded approach by default.<br />
        /// 从指定的URL下载文件，并使用指定的文件名将其保存到给定的路径。
        /// 默认情况下，此方法使用单线程方法。
        /// </summary>
        /// <param name="url">The URL of the file to download.<br />要下载的文件的URL。</param>
        /// <param name="savePath">The local directory where the file will be saved.<br />要保存文件的本地目录。</param>
        /// <param name="fileName">The name of the file to save as.<br />要保存为的文件名。</param>
        /// <param name="downloadType">The download type (default is SingleThread).<br />下载类型（默认为SingleThread单线程下载）。</param>
        public static void Download(string url, string savePath, string fileName, DownloadType downloadType = DownloadType.SingleThread) => DownloadCommon(url, savePath, fileName, 81920, downloadType);

        /// <summary>
        /// Downloads a file from the specified URL and saves it to the given path with the specified file name. 
        /// This method allows setting a custom buffer size and download type.<br />
        /// 从指定的URL下载文件，并使用指定的文件名将其保存到给定的路径。
        /// 此方法允许设置自定义缓冲区大小和下载类型。
        /// </summary>
        /// <param name="url">The URL of the file to download.<br />要下载的文件的URL。</param>
        /// <param name="savePath">The local directory where the file will be saved.<br />要保存文件的本地目录。</param>
        /// <param name="fileName">The name of the file to save as.<br />要保存为的文件名。</param>
        /// <param name="bufferSize">The size of the buffer to use for downloading (default is 81920 bytes).<br />用于下载的缓冲区大小（默认为81920字节）。</param>
        /// <param name="downloadType">The download type (default is SingleThread).<br />下载类型（默认为SingleThread单线程下载）。</param>
        public static void Download(string url, string savePath, string fileName, int bufferSize = 81920, DownloadType downloadType = DownloadType.SingleThread) => DownloadCommon(url, savePath, fileName, bufferSize, downloadType);

        /// <summary>
        /// Asynchronously downloads a file from the specified URL and saves it to the given path with the specified file name. 
        /// This method uses a single-threaded approach by default.<br />
        /// 从指定的URL异步下载文件，并以指定的文件名将其保存到给定的路径。
        /// 此方法默认使用单线程方法。
        /// </summary>
        /// <param name="url">The URL of the file to download.<br />要下载的文件的URL。</param>
        /// <param name="savePath">The local directory where the file will be saved.<br />要保存文件的本地目录。</param>
        /// <param name="fileName">The name of the file to save as.<br />要保存为的文件名。</param>
        /// <param name="downloadType">The download type (default is SingleThread).<br />下载类型（默认为SingleThread单线程下载）。</param>
        /// <returns>A Task that represents the asynchronous operation.<br />表示异步操作的Task。</returns>
        public static async Task DownloadAsync(string url, string savePath, string fileName, DownloadType downloadType = DownloadType.SingleThread) => await DownloadCommonAsync(url, savePath, fileName, 81920, downloadType);

        /// <summary>
        /// Asynchronously downloads a file from the specified URL and saves it to the given path with the specified file name. 
        /// This method allows setting a custom buffer size and download type.<br />
        /// 从指定的URL异步下载文件，并以指定的文件名将其保存到给定的路径。
        /// 此方法允许设置自定义缓冲区大小和下载类型。
        /// </summary>
        /// <param name="url">The URL of the file to download.<br />要下载的文件的URL。</param>
        /// <param name="savePath">The local directory where the file will be saved.<br />要保存文件的本地目录。</param>
        /// <param name="fileName">The name of the file to save as.<br />要保存为的文件名。</param>
        /// <param name="bufferSize">The size of the buffer to use for downloading (default is 81920 bytes).<br />用于下载的缓冲区大小（默认为81920字节）。</param>
        /// <param name="downloadType">The download type (default is SingleThread).<br />下载类型（默认为SingleThread单线程下载）。</param>
        /// <returns>A Task that represents the asynchronous operation.<br />表示异步操作的Task。</returns>
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

    /// <summary>
    /// Download type.
    /// </summary>
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
