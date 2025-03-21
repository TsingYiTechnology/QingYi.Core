using QingYi.Core.Network.Download;

namespace DownloadTest
{
    internal class Program
    {
        private static readonly string downloadLink = "https://filesamples.com/samples/document/csv/sample4.csv";

        static async Task Main(string[] args)
        {
            SingleThread();
            Console.WriteLine("单线程完成");
            Thread.Sleep(250);

            await MultiThread();
            Console.WriteLine("多线程完成");

            Console.ReadLine();
        }

        static void SingleThread() => SingleThreadDownload.Download(downloadLink, "./", "single-thread.csv");

        static async Task MultiThread()
        {
            var downloader = new MultiThreadDownload(new Uri(downloadLink), "./", "multi-thread.csv");

            await downloader.StartDownloadAsync();

            // 获取进度
            //Console.WriteLine($"Download progress: {downloader.GetProgress():P}");
        }
    }
}
