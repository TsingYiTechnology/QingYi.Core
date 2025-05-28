using System;
using System.IO;
using System.IO.Compression;

namespace QingYi.Core.Compression
{
    /// <summary>
    /// Provides DEFLATE compression and decompression functionality
    /// </summary>
    public class Deflate
    {
        /// <summary>
        /// Compresses input data using DEFLATE algorithm
        /// </summary>
        /// <param name="data">Input byte array to compress</param>
        /// <returns>
        /// Compressed byte array.
        /// Returns empty array if input is null or empty.
        /// </returns>
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

        /// <summary>
        /// Compresses input data using DEFLATE algorithm and outputs as MemoryStream
        /// </summary>
        /// <param name="data">Input byte array to compress</param>
        /// <param name="memoryStream">Output stream containing compressed data</param>
        /// <remarks>
        /// <para>Important usage notes:</para>
        /// <list type="bullet">
        /// <item>Caller is responsible for disposing the returned MemoryStream</item>
        /// <item>If input is null or empty, returns an empty MemoryStream</item>
        /// <item>Returned stream position is set to 0 for immediate reading</item>
        /// </list>
        /// </remarks>
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

        /// <summary>
        /// Decompresses DEFLATE-compressed data
        /// </summary>
        /// <param name="compressedData">Compressed byte array</param>
        /// <returns>
        /// Decompressed byte array.
        /// Returns empty array if input is null or empty.
        /// </returns>
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

        /// <summary>
        /// Decompresses DEFLATE-compressed data and outputs as MemoryStream
        /// </summary>
        /// <param name="compressedData">Compressed byte array</param>
        /// <param name="memoryStream">Output stream containing decompressed data</param>
        /// <remarks>
        /// <para>Important usage notes:</para>
        /// <list type="bullet">
        /// <item>Caller is responsible for disposing the returned MemoryStream</item>
        /// <item>If input is null or empty, returns an empty MemoryStream</item>
        /// <item>Returned stream position is set to 0 for immediate reading</item>
        /// </list>
        /// </remarks>
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
