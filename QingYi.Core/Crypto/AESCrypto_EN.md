### AES Encryption and Decryption Usage

This document explains three ways to use the `AESCrypto` class: **static methods**, **instance methods**, and **using statement**.

---

### 1. Static Methods (Recommended for simple scenarios)  
Use the `AESCryptoHelper` static class for one-off operations. Automatically manages resources.  
**Best for**: Quick single encryption/decryption operations.

#### Encryption Example
```csharp
byte[] data = Encoding.UTF8.GetBytes("Hello, AES!");
byte[] key = new byte[32]; // 16, 24, or 32 bytes
byte[] iv = new byte[16];  // Must be 16 bytes

// Generate random values (real usage)
RandomNumberGenerator.Fill(key);
RandomNumberGenerator.Fill(iv);

// Static encryption
byte[] encrypted = AESCryptoHelper.Encrypt(data, key, iv);
```

#### Decryption Example
```csharp
// Static decryption
byte[] decrypted = AESCryptoHelper.Decrypt(encrypted, key, iv);
string result = Encoding.UTF8.GetString(decrypted); // "Hello, AES!"
```

---

### 2. Instance Methods (Manual resource management)  
Create an `AESCrypto` instance for repeated operations with the same key.  
**Warning**: You **must** call `Dispose()` to avoid leaks.

#### Encryption Example
```csharp
var crypto = new AESCrypto(); // Auto-generates key/IV
crypto.Key = key; // Custom key (optional)
crypto.IV = iv;   // Custom IV (optional)

byte[] data = Encoding.UTF8.GetBytes("Hello, AES!");
byte[] encrypted = crypto.Encrypt(data);

// Critical: Manually release resources
crypto.Dispose();
```

#### Decryption Example
```csharp
var crypto = new AESCrypto();
crypto.Key = key;
crypto.IV = iv;

byte[] decrypted = crypto.Decrypt(encrypted);
crypto.Dispose(); // Required!
```

---

### 3. Using Statement (Recommended best practice)  
Automatically handles resource disposal with `using`. Safest for all scenarios.

#### Encryption Example
```csharp
byte[] data = Encoding.UTF8.GetBytes("Hello, AES!");

using (var crypto = new AESCrypto())
{
    crypto.Key = key; // Set key
    crypto.IV = iv;   // Set IV
    byte[] encrypted = crypto.Encrypt(data);
} // Auto-disposal here
```

#### Decryption Example
```csharp
using (var crypto = new AESCrypto())
{
    crypto.Key = key;
    crypto.IV = iv;
    byte[] decrypted = crypto.Decrypt(encrypted);
} // Resources released automatically
```

---

### Key Specifications
1. **Key Requirements**:
   - 16 bytes (128-bit)
   - 24 bytes (192-bit)
   - 32 bytes (256-bit)
   
2. **IV Requirement**:  
   Fixed 16 bytes

3. **Resource Management**:
   | Method          | Disposal Mechanism       | Risk Level |
   |-----------------|--------------------------|------------|
   | Static          | Automatic                | None       |
   | Instance        | Manual (`Dispose()`)     | High       |
   | `using`         | Automatic                | None       |

4. **Default Configuration**:
   - Cipher Mode: `CipherMode.CBC`
   - Padding Mode: `PaddingMode.PKCS7`

> 💡 **Best Practice Recommendations**:  
> - Single operation → Static method  
> - Batch operations → `using` statement  
> - Never use instance methods without manual `Dispose()`  
> - Always generate keys/IVs with `RandomNumberGenerator`
