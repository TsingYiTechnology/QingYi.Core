using System;
using System.Threading;
using System.Threading.Tasks;

namespace QingYi.Core.FileUtility.IO
{
    public static class FileWriterStatic
    {
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public static void Write(byte[] data, string filePath)
        {
            using var writer = new FileWriter(filePath);
            writer.Write(data.AsSpan());
        }

        public static async Task WriteAsync(byte[] data, string filePath)
        {
            await using var writer = new FileWriter(filePath);
            await writer.WriteAsync(data.AsMemory());
        }

#elif NETSTANDARD2_0
        public static void Write(byte[] data, string filename)
        {
            using (var writer = new FileWriter(filename))
            {
                writer.Write(data, 0, data.Length);
            }
        }

        public static async Task WriteAsync(byte[] data, string filename)
        {
            using (var writer = new FileWriter(filename))
            {
                await writer.WriteAsync(data, 0, data.Length, CancellationToken.None);
                await writer.DisposeAsync();
            }
        }
#endif
    }
}
