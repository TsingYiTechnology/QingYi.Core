### DESCrypto Usage Documentation

#### Overview
`DESCrypto` provides DES encryption/decryption functionality through instance methods and static helpers. It implements the `ICrypto` interface and supports:
- ECB/CBC/CFB cipher modes (default: CBC)
- PKCS7/ISO10126/ANSIX923 padding (default: PKCS7)
- Byte arrays, streams, and memory-efficient spans (modern .NET)

---

### 1. Instance Usage
Create a reusable encryptor/decryptor object:

```csharp
using QingYi.Core.Crypto;

// Initialize with default CBC + PKCS7
using var des = new DESCrypto();

// Set custom key/IV (MUST be 8 bytes)
des.Key = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF };
des.IV = new byte[8]; // Zero IV

// Generate random key/IV
des.GenerateKeyIV();

// Encrypt/decrypt byte arrays
byte[] encrypted = des.Encrypt(Encoding.UTF8.GetBytes("Secret Data"));
byte[] decrypted = des.Decrypt(encrypted);

// Encrypt/decrypt streams
using (var input = new MemoryStream(plainData))
using (var output = new MemoryStream())
{
    des.Encrypt(input, output); // Output contains encrypted data
}

// Modern .NET (Span<T> support)
#if NETCOREAPP3_0_OR_GREATER
ReadOnlySpan<byte> data = stackalloc byte[] { 1, 2, 3 };
byte[] enc = des.Encrypt(data);
#endif
```

---

### 2. Static Helper Usage
One-off operations without instantiating:

```csharp
byte[] key = new byte[8]; // 8-byte key
byte[] iv = new byte[8];  // 8-byte IV

// Byte array operations
byte[] data = Encoding.UTF8.GetBytes("Data");
byte[] enc = DESCryptoHelper.Encrypt(data, key, iv);
byte[] dec = DESCryptoHelper.Decrypt(enc, key, iv);

// Stream operations
using (var input = new MemoryStream(data))
using (var output = new MemoryStream())
{
    DESCryptoHelper.Encrypt(input, output, key, iv);
    // Encrypted in output
}

// Modern .NET (Span<T>)
#if NETCOREAPP3_0_OR_GREATER
ReadOnlySpan<byte> spanData = stackalloc byte[] { 4, 5, 6 };
byte[] encSpan = DESCryptoHelper.Encrypt(spanData, key, iv);
#endif
```

---

### Key Requirements
- **Key**: Exactly 8 bytes
- **IV**: Exactly 8 bytes
- **Validation**: Invalid lengths throw `ArgumentException`

---

### Important Notes
1. **Disposable**: Always wrap in `using` statements
2. **Security**: DES is considered weak; prefer AES for new systems
3. **Compatibility**: 
   - Uses `DESCryptoServiceProvider` for legacy .NET
   - Uses `DES.Create()` in .NET 6+
4. **Stream Handling**: Output streams are not closed/disposed automatically

---

### Error Handling
Check for common exceptions:
```csharp
try
{
    // Crypto operations
}
catch (ArgumentException ex) // Invalid key/IV
catch (CryptographicException ex) // Encryption/decryption failure
catch (ObjectDisposedException ex) // Using disposed object
```