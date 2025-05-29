以下是使用 `PPMd` 类进行文件压缩和解压缩的完整示例：

```csharp
using QingYi.Core.Compression;
using System;
using System.IO;

namespace PPMdExample
{
    class Program
    {
        static void Main(string[] args)
        {
            string originalFile = "document.txt";
            string compressedFile = "document.ppmd";
            string decompressedFile = "document_decompressed.txt";

            // 示例1：基本压缩
            CompressFile(originalFile, compressedFile);

            // 示例2：自定义参数压缩
            CompressWithCustomParams(originalFile, compressedFile);

            // 示例3：解压文件
            DecompressFile(compressedFile, decompressedFile);

            Console.WriteLine("操作完成！");
        }

        static void CompressFile(string inputPath, string outputPath)
        {
            using var input = File.OpenRead(inputPath);
            using var output = File.Create(outputPath);
            
            // 使用默认参数（压缩级别6，自动线程数，字典大小16MB，模型阶数6）
            using var compressor = new PPMd();
            compressor.Compress(input, output);
            
            Console.WriteLine($"文件已压缩: {inputPath} -> {outputPath}");
        }

        static void CompressWithCustomParams(string inputPath, string outputPath)
        {
            using var input = File.OpenRead(inputPath);
            using var output = File.Create(outputPath);
            
            // 自定义参数：
            // - 压缩级别：9（最高）
            // - 线程数：4
            // - 字典大小：64MB
            // - 模型阶数：8
            using var compressor = new PPMd(
                compressionLevel: 9,
                threadCount: 4,
                dictionarySize: 64 * 1024 * 1024,
                modelOrder: 8
            );
            
            compressor.Compress(input, output);
            Console.WriteLine($"文件已使用自定义参数压缩: {inputPath} -> {outputPath}");
        }

        static void DecompressFile(string inputPath, string outputPath)
        {
            using var input = File.OpenRead(inputPath);
            using var output = File.Create(outputPath);
            
            // 注意：解压时参数必须与压缩时一致！
            // 使用与压缩时相同的参数创建实例
            using var decompressor = new PPMd(
                compressionLevel: 9,
                threadCount: 4,
                dictionarySize: 64 * 1024 * 1024,
                modelOrder: 8
            );
            
            decompressor.Decompress(input, output);
            Console.WriteLine($"文件已解压: {inputPath} -> {outputPath}");
        }
    }
}
```

### 关键注意事项：

1. **参数一致性**：
   - 解压时必须使用与压缩时**完全相同的参数**（压缩级别、线程数、字典大小、模型阶数）
   - 参数存储在文件头中，不匹配会导致 `InvalidDataException`

2. **参数范围限制**：
   ```csharp
   // 有效模型阶数：2-16
   new PPMd(modelOrder: 6);  // 有效
   new PPMd(modelOrder: 20); // 抛出 ArgumentException

   // 有效字典大小：1MB-512MB
   new PPMd(dictionarySize: 64 * 1024 * 1024);  // 有效 (64MB)
   new PPMd(dictionarySize: 1024);              // 抛出 ArgumentException
   ```

3. **多线程行为**：
   - 当 `threadCount = 0` 或未指定时：自动使用 CPU 核心数
   - 当 `threadCount = 1` 时：使用单线程模式
   - 当 `threadCount > 1` 时：启用并行压缩/解压

4. **异常处理**：
   ```csharp
   try
   {
       compressor.Decompress(input, output);
   }
   catch (InvalidDataException ex)
   {
       Console.WriteLine($"数据损坏或不匹配: {ex.Message}");
   }
   catch (ArgumentException ex)
   {
       Console.WriteLine($"参数错误: {ex.Message}");
   }
   ```

### 文件格式说明：
压缩后的文件包含：
1. **10字节头部**：
   ```csharp
   [0] = 压缩级别 (byte)
   [1] = 线程数 (byte)
   [2-5] = 字典大小 (int)
   [6] = 模型阶数 (byte)
   [7-9] = 保留字节
   ```
2. **数据块**（多线程模式下）：
   ```csharp
   [块1原始大小] (4字节 int)
   [块1压缩大小] (4字节 int)
   [压缩数据]
   [块2原始大小]...
   ```

> **提示**：对于小文件（<10MB），建议使用单线程模式（`threadCount=1`）以避免并行处理开销。对于大文件（>100MB），多线程可显著提升速度。