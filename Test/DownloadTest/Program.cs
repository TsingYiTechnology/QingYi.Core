using QingYi.Core.FileUtility.IO;
using QingYi.Core.Network.Download;

namespace DownloadTest
{
    internal class Program
    {
        private static readonly string downloadLink = "https://www.nuget.org/api/v2/package/QingYi.Core/5.1.1";

        static async Task Main(string[] args)
        {
            await SingleThread();
        }

        static async Task SingleThread()
        {
            var data = SingleThreadDownload.Download(downloadLink);
            await FileWriterStatic.WriteAsync("single-thread.nupkg", data);
        }
    }
}
