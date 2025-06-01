Here is the complete English translation of the PPMd documentation:

```markdown
﻿Below is a complete example of file compression and decompression using the `PPMd` class:

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

            // Example 1: Basic compression
            CompressFile(originalFile, compressedFile);

            // Example 2: Custom parameter compression
            CompressWithCustomParams(originalFile, compressedFile);

            // Example 3: File decompression
            DecompressFile(compressedFile, decompressedFile);

            Console.WriteLine("Operation completed!");
        }

        static void CompressFile(string inputPath, string outputPath)
        {
            using var input = File.OpenRead(inputPath);
            using var output = File.Create(outputPath);
            
            // Use default parameters (compression level 6, auto thread count, 16MB dictionary size, model order 6)
            using var compressor = new PPMd();
            compressor.Compress(input, output);
            
            Console.WriteLine($"File compressed: {inputPath} -> {outputPath}");
        }

        static void CompressWithCustomParams(string inputPath, string outputPath)
        {
            using var input = File.OpenRead(inputPath);
            using var output = File.Create(outputPath);
            
            // Custom parameters:
            // - Compression level: 9 (maximum)
            // - Thread count: 4
            // - Dictionary size: 64MB
            // - Model order: 8
            using var compressor = new PPMd(
                compressionLevel: 9,
                threadCount: 4,
                dictionarySize: 64 * 1024 * 1024,
                modelOrder: 8
            );
            
            compressor.Compress(input, output);
            Console.WriteLine($"File compressed with custom parameters: {inputPath} -> {outputPath}");
        }

        static void DecompressFile(string inputPath, string outputPath)
        {
            using var input = File.OpenRead(inputPath);
            using var output = File.Create(outputPath);
            
            // Important: Decompression parameters must match compression parameters!
            // Create instance with same parameters used during compression
            using var decompressor = new PPMd(
                compressionLevel: 9,
                threadCount: 4,
                dictionarySize: 64 * 1024 * 1024,
                modelOrder: 8
            );
            
            decompressor.Decompress(input, output);
            Console.WriteLine($"File decompressed: {inputPath} -> {outputPath}");
        }
    }
}
```

### Key Considerations:

1. **Parameter Consistency**:
   - Decompression requires **exactly the same parameters** as compression (compression level, thread count, dictionary size, model order)
   - Parameters are stored in the file header - mismatches will cause `InvalidDataException`

2. **Parameter Range Constraints**:
   ```csharp
   // Valid model order: 2-16
   new PPMd(modelOrder: 6);  // Valid
   new PPMd(modelOrder: 20); // Throws ArgumentException

   // Valid dictionary size: 1MB-512MB
   new PPMd(dictionarySize: 64 * 1024 * 1024);  // Valid (64MB)
   new PPMd(dictionarySize: 1024);              // Throws ArgumentException
   ```

3. **Multithreading Behavior**:
   - When `threadCount = 0` or unspecified: Automatically uses number of CPU cores
   - When `threadCount = 1`: Uses single-thread mode
   - When `threadCount > 1`: Enables parallel compression/decompression

4. **Exception Handling**:
   ```csharp
   try
   {
       compressor.Decompress(input, output);
   }
   catch (InvalidDataException ex)
   {
       Console.WriteLine($"Data corrupted or parameter mismatch: {ex.Message}");
   }
   catch (ArgumentException ex)
   {
       Console.WriteLine($"Invalid parameter: {ex.Message}");
   }
   ```

### File Format Specification:
Compressed files contain:
1. **10-byte Header**:
   ```csharp
   [0] = Compression level (byte)
   [1] = Thread count (byte)
   [2-5] = Dictionary size (int)
   [6] = Model order (byte)
   [7-9] = Reserved bytes
   ```
2. **Data Blocks** (in multithreaded mode):
   ```csharp
   [Block 1 original size] (4-byte int)
   [Block 1 compressed size] (4-byte int)
   [Compressed data]
   [Block 2 original size]...
   ```

> **Recommendation**: For small files (<10MB), use single-thread mode (`threadCount=1`) to avoid parallel processing overhead. For large files (>100MB), multithreading provides significant speed improvements.
