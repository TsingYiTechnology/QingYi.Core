using QingYi.Core.Network.Download;

namespace DownloadTest
{
    internal class Program
    {
        private static readonly string downloadLink = "https://www.nuget.org/api/v2/package/QingYi.Core/5.1.1";

        static async Task Main(string[] args)
        {
            SingleThread();
        }

        static void SingleThread()
        {
            SingleThreadDownload.Download(downloadLink, "./", "single-thread.nupkg");
        }
    }
}
