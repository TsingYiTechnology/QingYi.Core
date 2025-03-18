using System;
using System.Threading.Tasks;

namespace QingYi.Core.FileUtility.IO
{
    public static class FileWriterStatic
    {
        public static void Write(byte[] data, string filePath)
        {
            using var writer = new FileWriter(filePath);
            writer.Write(data.AsSpan());
        }

        public static void Write(string filePath, byte[] data) => Write(data, filePath);

        public static async Task WriteAsync(byte[] data, string filePath) => await Task.Run(() => Write(data, filePath));

        public static async Task WriteAsync(string filePath, byte[] data) => await Task.Run(() => Write(data, filePath));
    }
}
