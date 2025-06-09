### TripleDES Encryption/Decryption Usage Documentation

The library provides three flexible approaches for TripleDES encryption and decryption:

#### 1. Static Helper Methods (Simplest)
Use `TripleDESHelper` for one-time operations without managing instances:
```csharp
// Configuration
byte[] key = new byte[24];  // 16 or 24 bytes
byte[] iv = new byte[8];    // Must be 8 bytes
byte[] data = Encoding.UTF8.GetBytes("Secret data");

// Encryption
byte[] encrypted = TripleDESHelper.Encrypt(data, key, iv);

// Decryption
byte[] decrypted = TripleDESHelper.Decrypt(encrypted, key, iv);
```

#### 2. Instance-based Usage
Create reusable instances for multiple operations:
```csharp
var crypto = new TripleDESCrypto();
crypto.Key = myKey;  // Set custom key
crypto.IV = myIV;    // Set custom IV

// Encrypt/decrypt multiple times
byte[] cipherText = crypto.Encrypt(plainData);
byte[] recoveredText = crypto.Decrypt(cipherText);

// Remember to dispose later
crypto.Dispose();
```

#### 3. Using Statement (Recommended)
Ensure automatic cleanup with `IDisposable`:
```csharp
using (var crypto = new TripleDESCrypto(CipherMode.CBC, PaddingMode.PKCS7))
{
    // Uses auto-generated key/IV by default
    byte[] encrypted = crypto.Encrypt(data);
    
    // Stream operations
    using (var input = File.OpenRead("file.bin"))
    using (var output = File.Create("encrypted.bin"))
    {
        crypto.Encrypt(input, output);
    }
} // Automatic disposal here
```

### Key Features:
- **Key/IV Requirements**: 
  - Keys: 16 or 24 bytes
  - IV: Always 8 bytes
- **Automatic Generation**: `GenerateKeyIV()` creates compliant keys/IVs
- **Modes Support**: CBC (default), ECB, CFB, etc.
- **Stream Handling**: Built-in support for large data streams
- **Span Support**: Optimized for modern runtimes (NETStandard 2.1+)

> **Security Note**: Always store keys/IVs securely. Default mode is CBC with PKCS7 padding - use stronger modes like GCM for new projects where possible.
