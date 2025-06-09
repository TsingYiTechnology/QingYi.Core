### TripleDESCrypto 使用文档

本文档介绍三种使用 `TripleDESCrypto` 类进行加解密的方法：**静态调用**、**实例化调用**和 **`using` 语句调用**。

---

### 1. 静态调用（推荐简单场景）
通过 `TripleDESHelper` 静态类直接加解密，无需手动管理资源。  
**特点**：自动释放资源，代码简洁，适合单次操作。

#### 加密示例
```csharp
byte[] data = Encoding.UTF8.GetBytes("Hello, TripleDES!");
byte[] key = new byte[24]; // 16 或 24 字节密钥
byte[] iv = new byte[8];   // 8 字节 IV

// 生成随机密钥和 IV（实际使用时替换）
RandomNumberGenerator.Fill(key);
RandomNumberGenerator.Fill(iv);

// 静态加密
byte[] encrypted = TripleDESHelper.Encrypt(data, key, iv);
```

#### 解密示例
```csharp
// 静态解密
byte[] decrypted = TripleDESHelper.Decrypt(encrypted, key, iv);
string result = Encoding.UTF8.GetString(decrypted); // "Hello, TripleDES!"
```

---

### 2. 实例化调用（需手动释放）
创建 `TripleDESCrypto` 实例，适合多次操作同一密钥。  
**注意**：必须调用 `Dispose()` 释放资源。

#### 加密示例
```csharp
var crypto = new TripleDESCrypto(); // 自动生成密钥和 IV
crypto.Key = key; // 自定义密钥（可选）
crypto.IV = iv;   // 自定义 IV（可选）

byte[] data = Encoding.UTF8.GetBytes("Hello, TripleDES!");
byte[] encrypted = crypto.Encrypt(data);

// 手动释放资源
crypto.Dispose();
```

#### 解密示例
```csharp
var crypto = new TripleDESCrypto();
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
byte[] data = Encoding.UTF8.GetBytes("Hello, TripleDES!");

using (var crypto = new TripleDESCrypto())
{
    crypto.Key = key; // 自定义密钥
    crypto.IV = iv;   // 自定义 IV
    byte[] encrypted = crypto.Encrypt(data);
} // 自动调用 Dispose()
```

#### 解密示例
```csharp
using (var crypto = new TripleDESCrypto())
{
    crypto.Key = key;
    crypto.IV = iv;
    byte[] decrypted = crypto.Decrypt(encrypted);
} // 自动释放资源
```

---

### 关键注意事项
1. **密钥长度**：必须为 **16 字节（112 位）** 或 **24 字节（168 位）**。
2. **IV 长度**：固定 **8 字节**。
3. **资源释放**：
   - 静态方法：内部自动释放。
   - 实例化：必须手动调用 `Dispose()`。
   - `using`：自动释放，**推荐使用**。
4. **默认设置**：
   - 加密模式：`CipherMode.CBC`
   - 填充模式：`PaddingMode.PKCS7`

> 💡 **最佳实践建议**：  
> - 单次操作 → 静态方法  
> - 多次操作 → `using` 语句  
> - 避免手动实例化不释放资源
