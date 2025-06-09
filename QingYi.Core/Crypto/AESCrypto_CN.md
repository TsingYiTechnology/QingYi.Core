### AESCrypto 使用文档

本文档介绍三种使用 AES 加密的方法：**静态调用**、**实例化调用**和 **`using` 语句调用**。

---

### 1. 静态调用（推荐简单场景）
通过 `AESCryptoHelper` 静态类直接加解密，无需手动管理资源。  
**特点**：自动释放资源，代码简洁，适合单次操作。

#### 加密示例
```csharp
byte[] data = Encoding.UTF8.GetBytes("Hello, AES!");
byte[] key = new byte[32]; // 16/24/32 字节密钥
byte[] iv = new byte[16];  // 16 字节 IV

// 生成随机密钥和 IV（实际使用时替换）
RandomNumberGenerator.Fill(key);
RandomNumberGenerator.Fill(iv);

// 静态加密
byte[] encrypted = AESCryptoHelper.Encrypt(data, key, iv);
```

#### 解密示例
```csharp
// 静态解密
byte[] decrypted = AESCryptoHelper.Decrypt(encrypted, key, iv);
string result = Encoding.UTF8.GetString(decrypted); // "Hello, AES!"
```

---

### 2. 实例化调用（需手动释放）
创建 `AESCrypto` 实例，适合多次操作同一密钥。  
**注意**：必须调用 `Dispose()` 释放资源。

#### 加密示例
```csharp
var crypto = new AESCrypto(); // 自动生成密钥和 IV
crypto.Key = key; // 自定义密钥（16/24/32字节）
crypto.IV = iv;   // 自定义 IV（16字节）

byte[] data = Encoding.UTF8.GetBytes("Hello, AES!");
byte[] encrypted = crypto.Encrypt(data);

// 手动释放资源
crypto.Dispose();
```

#### 解密示例
```csharp
var crypto = new AESCrypto();
crypto.Key = key;
crypto.IV = iv;

byte[] decrypted = crypto.Decrypt(encrypted);
crypto.Dispose(); // 必须释放！
```

---

### 3. `using` 语句调用（推荐安全释放）
通过 `using` 自动释放资源，避免内存泄漏，**是最佳实践**。

#### 加密示例
```csharp
byte[] data = Encoding.UTF8.GetBytes("Hello, AES!");

using (var crypto = new AESCrypto())
{
    crypto.Key = key; // 自定义密钥
    crypto.IV = iv;   // 自定义 IV
    byte[] encrypted = crypto.Encrypt(data);
} // 自动调用 Dispose()
```

#### 解密示例
```csharp
using (var crypto = new AESCrypto())
{
    crypto.Key = key;
    crypto.IV = iv;
    byte[] decrypted = crypto.Decrypt(encrypted);
} // 自动释放资源
```

---

### 关键注意事项
1. **密钥长度**：
   - 必须为 **16 字节（128 位）**
   - **24 字节（192 位）** 或 **32 字节（256 位）**
2. **IV 长度**：固定 **16 字节**
3. **平台兼容性**：
   - .NET Framework：使用 `AesManaged`
   - .NET 6+：使用 `Aes.Create()`
4. **资源释放**：
   - 静态方法：内部自动释放
   - 实例化：必须手动调用 `Dispose()`
   - `using`：自动释放，**推荐使用**
5. **默认设置**：
   - 加密模式：`CipherMode.CBC`
   - 填充模式：`PaddingMode.PKCS7`

> 💡 **最佳实践建议**：  
> - 单次操作 → 静态方法  
> - 多次操作 → `using` 语句  
> - 避免手动实例化不释放资源

---

### 流处理示例（静态方法）
```csharp
// 文件加密
using (FileStream input = File.OpenRead("plain.txt"))
using (FileStream output = File.Create("encrypted.bin"))
{
    AESCryptoHelper.Encrypt(input, output, key, iv);
}

// 文件解密
using (FileStream input = File.OpenRead("encrypted.bin"))
using (FileStream output = File.Create("decrypted.txt"))
{
    AESCryptoHelper.Decrypt(input, output, key, iv);
}
```
