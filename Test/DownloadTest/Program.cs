using QingYi.Core.FileUtility.IO;
using QingYi.Core.Network.Download;

namespace DownloadTest
{
    internal class Program
    {
        private static readonly string downloadLink = "https://www.nuget.org/api/v2/package/QingYi.Core/5.1.1";

        static void Main(string[] args)
        {
            SingleThread();
        }

        static void SingleThread()
        {
            var data = SingleThreadDownload.Download(downloadLink);
            FileWriterStatic.WriteAsync("single-thread.nupkg", data);
        }
    }
}
