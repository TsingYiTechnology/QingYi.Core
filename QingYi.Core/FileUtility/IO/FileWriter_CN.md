### 一、.NET 5.0+ / .NET Standard 2.1+ 示例
```csharp
using QingYi.Core.FileUtility.IO;
using System;
using System.Threading.Tasks;

public class Net5Example
{
    public static async Task RunAsync()
    {
        // 同步写入示例
        using (var writer = new FileWriter("high_perf.dat"))
        {
            // 生成测试数据
            byte[] data = new byte[1024 * 1024]; // 1MB
            new Random().NextBytes(data);

            // 同步写入（自动处理缓冲）
            writer.Write(data.AsSpan());

            // 手动刷新
            writer.Flush();
        }

        // 异步写入示例
        await using (var writer = new FileWriter("async_high_perf.dat"))
        {
            byte[] asyncData = new byte[2048 * 1024]; // 2MB
            new Random().NextBytes(asyncData);

            // 异步写入
            await writer.WriteAsync(asyncData.AsMemory());

            // 直接写入大块数据
            await writer.WriteDirectAsync(asyncData.AsMemory());
        }

        // 高级用法：混合写入
        var writerAdv = new FileWriter("mixed.dat", bufferSize: 4096);
        try
        {
            // 多次小数据写入
            for (int i = 0; i < 100; i++)
            {
                writerAdv.Write(new byte[128]); // 自动缓冲
            }

            // 直接写入4MB数据（超过缓冲区大小）
            byte[] bigData = new byte[4 * 1024 * 1024];
            writerAdv.Write(bigData.AsSpan());
        }
        finally
        {
            await writerAdv.DisposeAsync();
        }
    }
}
```

### 二、.NET Standard 2.0 示例
```csharp
using QingYi.Core.FileUtility.IO;
using System;
using System.Threading.Tasks;

public class NetStandard20Example
{
    public static async Task RunAsync()
    {
        // 同步写入示例
        using (var writer = new FileWriter("standard20.dat"))
        {
            byte[] data = new byte[1024 * 512]; // 512KB
            new Random().NextBytes(data);

            // 完整数组写入
            writer.Write(data, 0, data.Length);

            // 分段写入示例
            writer.Write(data, 0, 256);     // 写入前256字节
            writer.Write(data, 256, 512);   // 写入后续512字节
        }

        // 异步写入示例
        using (var writer = new FileWriter("async_standard20.dat"))
        {
            byte[] asyncData = new byte[2 * 1024 * 1024]; // 2MB
            
            // 完整异步写入
            await writer.WriteAsync(asyncData, 0, asyncData.Length, CancellationToken.None);

            // 直接写入大块数据
            await writer.WriteDirectAsync(asyncData, 0, asyncData.Length, CancellationToken.None);
        }

        // 手动控制缓冲区示例
        var writerManual = new FileWriter("manual.dat", bufferSize: 16384);
        try
        {
            // 写入刚好填满缓冲区
            byte[] fullBuffer = new byte[16384];
            writerManual.Write(fullBuffer, 0, fullBuffer.Length);

            // 再写入1字节触发自动刷新
            writerManual.Write(new byte[1], 0, 1);
        }
        finally
        {
            writerManual.Dispose();
        }
    }
}
```

### 三、通用最佳实践
1. **缓冲区选择原则**
```csharp
// 根据典型写入大小选择缓冲区
var smallWriter = new FileWriter("small.dat", bufferSize: 4096);  // 适合4KB级写入
var largeWriter = new FileWriter("large.dat", bufferSize: 65536); // 适合64KB级写入
```

2. **异常处理模式**
```csharp
try
{
    using var writer = new FileWriter("important.dat");
    writer.Write(GetCriticalData());
}
catch (IOException ex)
{
    Console.WriteLine($"文件操作失败: {ex.Message}");
}
catch (UnauthorizedAccessException)
{
    Console.WriteLine("权限不足");
}
```

3. **性能敏感场景**
```csharp
// 重用写入器实例
using var writer = new FileWriter("reuse.dat");
for (int i = 0; i < 1000; i++)
{
    writer.Write(GetNextDataChunk());
    if (i % 100 == 0) writer.Flush();
}
```

### 四、版本差异说明
| 特性                | .NET 5.0+/Std2.1+               | .NET Standard 2.0            |
|---------------------|----------------------------------|------------------------------|
| 写入方法签名        | `Write(ReadOnlySpan<byte>)`     | `Write(byte[], int, int)`    |
| 异步返回值          | `ValueTask`                     | `Task`                       |
| 内存操作            | 支持 Span/Memory API            | 使用传统数组操作             |
| 资源释放            | 支持 `await using` 语法         | 需手动调用 `DisposeAsync`    |
| 直接写入实现        | 使用内存映射+Span               | 使用 Marshal.Copy            |

### 五、跨版本兼容技巧
```csharp
public void WriteUniversal(byte[] data)
{
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    writer.Write(data.AsSpan());
#elif NETSTANDARD2_0
    writer.Write(data, 0, data.Length);
#endif
}

public async Task WriteUniversalAsync(byte[] data)
{
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    await writer.WriteAsync(data.AsMemory());
#elif NETSTANDARD2_0
    await writer.WriteAsync(data, 0, data.Length);
#endif
}
```
