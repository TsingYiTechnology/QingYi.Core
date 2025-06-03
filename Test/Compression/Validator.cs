using QingYi.Core.Compression.Unsafe.LZMA;

namespace Compression
{
    internal class Validator
    {
        public static bool Check(string origin, string de) => origin == de;

        public static bool Check(byte[] origin, byte[] de) => BitConverter.ToString(origin) == BitConverter.ToString(de);

        public static bool LZMACheck()
        {
            // 创建压缩器（可配置参数）
            var compressor = new LzmaCompressor(
                compressionLevel: 5,       // 压缩级别 (0-9)
                numThreads: 4,             // 线程数 (0=自动)
                dictionarySize: 1 << 24,   // 字典大小 (默认 16MB)
                wordSize: 32,               // 单词大小 (8-256)
                solid: false,               // 是否启用固实模式
                solidBlockSize: 1 << 28     // 固实块大小 (默认 256MB)
            );

            // 执行压缩
            using (var input = File.OpenRead("text.txt"))
            using (var output = File.Create("lzma_compressed.lzma"))
            {
                compressor.Compress(input, output);
            }

            // 创建解压器
            var decompressor = new LzmaDecompressor();

            // 执行解压
            using (var input = File.OpenRead("lzma_compressed.lzma"))
            using (var output = File.Create("lzma_decompressed.txt"))
            {
                decompressor.Decompress(input, output);
            }

            if(File.ReadAllText("text.txt") == File.ReadAllText("lzma_decompressed.txt"))
            {
                return true;
            }
            return false;
        }
    }
}
