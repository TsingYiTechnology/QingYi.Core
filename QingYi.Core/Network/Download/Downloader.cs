using System;

namespace QingYi.Core.Network.Download
{
    public class Downloader
    {
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
