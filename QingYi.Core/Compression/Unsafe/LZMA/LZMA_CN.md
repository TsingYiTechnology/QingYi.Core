### LZMA 压缩库使用方法

#### 1. 压缩数据 (`LzmaCompressor`)
```csharp
using QingYi.Core.Compression.Unsafe.LZMA;
using System.IO;

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
using (var input = File.OpenRead("original.bin"))
using (var output = File.Create("compressed.lzma"))
{
    compressor.Compress(input, output);
}
```

#### 2. 解压数据 (`LzmaDecompressor`)
```csharp
using QingYi.Core.Compression.Unsafe.LZMA;
using System.IO;

// 创建解压器
var decompressor = new LzmaDecompressor();

// 执行解压
using (var input = File.OpenRead("compressed.lzma"))
using (var output = File.Create("decompressed.bin"))
{
    decompressor.Decompress(input, output);
}
```

---

### 关键类说明

| **类名** | **功能** | **主要方法** |
|---------|---------|-------------|
| `LzmaCompressor` | 压缩数据 | `Compress(Stream input, Stream output)` |
| `LzmaDecompressor` | 解压数据 | `Decompress(Stream input, Stream output)` |

---

### 构造函数参数详解 (`LzmaCompressor`)

| **参数** | **类型** | **默认值** | **说明** |
|----------|----------|------------|----------|
| `compressionLevel` | `int` | `5` | 压缩级别 (0=最快, 9=最佳) |
| `numThreads` | `int` | `0` (自动) | 并行处理线程数 |
| `dictionarySize` | `int` | `1 << 24` (16MB) | 字典大小 (64KB-4096MB) |
| `wordSize` | `int` | `32` | 匹配查找深度 (8-256) |
| `solid` | `bool` | `false` | 是否启用固实压缩模式 |
| `solidBlockSize` | `int` | `1 << 28` (256MB) | 固实块大小 |

---

### 技术说明
1. **环境要求**
   - 仅支持 **.NET 8+**
   - 建议在项目文件中启用不安全代码：
     ```xml
     <PropertyGroup>
         <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
     </PropertyGroup>
     ```

2. **性能特性**
   - 自动使用硬件加速 (AVX2/SSE2)
   - 多线程并行处理
   - 支持大文件分块处理

3. **异常处理**
   - 无效数据会抛出 `InvalidDataException`
   - 参数错误会抛出 `ArgumentOutOfRangeException`

---

> ⚠️ 注意：解压时无需手动设置参数，压缩头信息会自动从输入流中读取。