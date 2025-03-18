using System;

namespace QingYi.Core.FileUtility.IO
{
    public static class FileWriterStatic
    {
        public static void Write(byte[] data, string filePath)
        {
            using var writer = new FileWriter(filePath);
            writer.Write(data.AsSpan());
        }

        public static void Write(string filePath, byte[] data)
        {
            using var writer = new FileWriter(filePath);
            writer.Write(data.AsSpan());
        }

        public static async void WriteAsync(byte[] data, string filePath)
        {
            await using var writer = new FileWriter(filePath);
            await writer.WriteAsync(data);
        }

        public static async void WriteAsync(string filePath, byte[] data)
        {
            await using var writer = new FileWriter(filePath);
            await writer.WriteAsync(data);
        }
    }
}
