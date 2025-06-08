### DESCrypto 使用文档  

`DESCrypto` 类实现了 `ICrypto` 接口，提供 **DES 对称加密/解密** 功能。支持多种加密模式（CBC、ECB 等）和填充模式（PKCS7、None 等），并提供静态快捷方法。

---

#### **核心功能**
1. **加密/解密字节数组**
2. **加密/解密数据流（Stream）**
3. **自动生成密钥（Key）和初始化向量（IV）**
4. **静态快捷方法（无需手动管理实例）**

---

#### **基础用法（实例模式）**
```csharp
using QingYi.Core.Crypto;

// 1. 创建实例（默认 CBC 模式 + PKCS7 填充）
using var des = new DESCrypto();

// 2. 自动生成密钥和 IV（构造函数已自动调用）
// 或手动指定（必须 8 字节）
des.Key = new byte[8] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF };
des.IV = new byte[8] { 0xFE, 0xDC, 0xBA, 0x98, 0x76, 0x54, 0x32, 0x10 };

// 3. 加密字节数组
byte[] plainData = Encoding.UTF8.GetBytes("Hello, DES!");
byte[] encrypted = des.Encrypt(plainData);

// 4. 解密字节数组
byte[] decrypted = des.Decrypt(encrypted);
Console.WriteLine(Encoding.UTF8.GetString(decrypted)); // 输出: "Hello, DES!"
```

---

#### **流加密/解密**
```csharp
using (FileStream input = File.OpenRead("original.txt"))
using (FileStream output = File.Create("encrypted.des"))
{
    // 加密流
    des.Encrypt(input, output);
}

using (FileStream input = File.OpenRead("encrypted.des"))
using (FileStream output = File.Create("decrypted.txt"))
{
    // 解密流
    des.Decrypt(input, output);
}
```

---

#### **静态快捷方法（推荐）**
```csharp
// 1. 准备密钥和 IV（必须 8 字节）
byte[] key = new byte[8] { ... };
byte[] iv = new byte[8] { ... };

// 2. 加密/解密字节数组
byte[] encrypted = DESCryptoHelper.Encrypt(
    data: Encoding.UTF8.GetBytes("静态方法真方便！"),
    key: key,
    iv: iv
);

byte[] decrypted = DESCryptoHelper.Decrypt(encrypted, key, iv);

// 3. 加密/解密文件流
using (FileStream inStream = File.OpenRead("file.txt"))
using (FileStream outStream = File.Create("file.enc"))
{
    DESCryptoHelper.Encrypt(inStream, outStream, key, iv);
}
```

---

#### **高级用法**
##### 自定义加密模式和填充
```csharp
// 使用 ECB 模式 + Zero 填充
var des = new DESCrypto(
    cipherMode: CipherMode.ECB, 
    paddingMode: PaddingMode.Zeros
);
```

##### 内存高效操作（.NET Core 3.0+）
```csharp
#if NETCOREAPP3_0_OR_GREATER
// 直接操作 Span<byte>
ReadOnlySpan<byte> dataSpan = stackalloc byte[] { 0xAA, 0xBB, 0xCC };
byte[] encrypted = des.Encrypt(dataSpan);
#endif
```

---

#### **重要注意事项**
1. **密钥与 IV 长度**  
   - `Key` 和 `IV` **必须为 8 字节**，否则抛出 `ArgumentException`。
   - 建议使用 `GenerateKeyIV()` 生成随机值。

2. **算法安全性**  
   - DES 已不够安全，仅适用于兼容旧系统。
   - 生产环境建议使用 `AESCrypto`（AES-256）。

3. **资源释放**  
   - `DESCrypto` 实现了 `IDisposable`，务必使用 `using` 语句。

4. **跨平台兼容性**  
   - 自动适配不同 .NET 版本：
     - .NET 6+：使用 `DES.Create()`
     - 旧版本：回退到 `DESCryptoServiceProvider`

---

#### **异常处理**
- 密钥/IV 长度错误 → `ArgumentException`
- 加密数据损坏 → `CryptographicException`
- 流操作错误 → `IOException`

```csharp
try
{
    var encrypted = DESCryptoHelper.Encrypt(data, key, iv);
}
catch (ArgumentException ex)
{
    Console.WriteLine($"密钥错误: {ex.Message}");
}
catch (CryptographicException ex)
{
    Console.WriteLine($"加密失败: {ex.Message}");
}
```

---

#### **示例完整代码**
```csharp
using QingYi.Core.Crypto;
using System;
using System.Text;

public class Program
{
    public static void Main()
    {
        // 静态方法示例
        byte[] key = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        byte[] iv = { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 };

        string text = "你好，世界！";
        byte[] data = Encoding.UTF8.GetBytes(text);

        // 加密
        byte[] encrypted = DESCryptoHelper.Encrypt(data, key, iv);
        
        // 解密
        byte[] decrypted = DESCryptoHelper.Decrypt(encrypted, key, iv);
        Console.WriteLine(Encoding.UTF8.GetString(decrypted)); // 输出: "你好，世界！"
    }
}
```
