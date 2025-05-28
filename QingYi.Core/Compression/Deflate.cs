using System;
using System.IO;
using System.IO.Compression;

namespace QingYi.Core.Compression
{
    public class Deflate
    {
        public static byte[] Compress(byte[] data)
        {
            // 如果输入为空数组，直接返回空数组
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            using (var outputStream = new MemoryStream())
            {
                // 创建压缩流（注意：使用CompressionMode.Compress模式）
                using (var deflateStream = new DeflateStream(outputStream, CompressionMode.Compress))
                {
                    // 写入原始数据到压缩流
                    deflateStream.Write(data, 0, data.Length);
                } // 这里using结束时会自动flush并关闭流

                // 返回压缩后的字节数组
                return outputStream.ToArray();
            }
        }

        public static void Compress(byte[] data, out MemoryStream memoryStream)
        {
            // 如果输入为空，直接返回Null
            if (data == null || data.Length == 0)
                memoryStream = (MemoryStream)Stream.Null;

            using (var outputStream = new MemoryStream())
            {
                // 创建压缩流（注意：使用CompressionMode.Compress模式）
                using (var deflateStream = new DeflateStream(outputStream, CompressionMode.Compress))
                {
                    // 写入原始数据到压缩流
                    deflateStream.Write(data, 0, data.Length);
                } // 这里using结束时会自动flush并关闭流

                // 返回压缩后的字节数组
                memoryStream = outputStream;
            }
        }

        public static byte[] Decompress(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0)
                return Array.Empty<byte>();

            using (var inputStream = new MemoryStream(compressedData))
            using (var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                deflateStream.CopyTo(outputStream);
                return outputStream.ToArray();
            }
        }

        public static void Decompress(byte[] compressedData, out MemoryStream memoryStream)
        {
            if (compressedData == null || compressedData.Length == 0)
                memoryStream = (MemoryStream)Stream.Null;

            using (var inputStream = new MemoryStream(compressedData))
            using (var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                deflateStream.CopyTo(outputStream);
                memoryStream = outputStream;
            }
        }
    }
}
