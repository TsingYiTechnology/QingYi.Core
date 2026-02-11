# AesCrypto - AES Encryption/Decryption Library

## Overview

`AesCrypto` is a comprehensive AES (Advanced Encryption Standard) cryptographic implementation that provides encryption and decryption functionality with support for multiple cipher modes including CBC, ECB, CFB, and authenticated encryption with GCM mode. The class implements the `ICrypto` interface and offers both synchronous and asynchronous operations for various data types including byte arrays, streams, and strings.

## Features

- ✅ Supports AES-128, AES-192, and AES-256 key sizes
- ✅ Multiple cipher modes: CBC, ECB, CFB, GCM
- ✅ Authenticated encryption with GCM mode
- ✅ Thread-safe implementation
- ✅ Stream support for large files
- ✅ String and Base64/Hex encoding helpers
- ✅ Automatic IV generation and management
- ✅ Secure key clearing and resource disposal

## Installation

Ensure you have the following namespace reference:

```csharp
using QingYi.Core.Crypto;
```

## Basic Usage

### Creating an Instance

#### From a Byte Array Key
```csharp
// Create with 256-bit key
byte[] key = new byte[32];
RandomNumberGenerator.Fill(key);
using var aes = new AesCrypto(key);

// Specify mode and padding
using var aesCbc = new AesCrypto(key, AesCrypto.ExtendedCipherMode.CBC);
using var aesGcm = new AesCrypto(key, AesCrypto.ExtendedCipherMode.GCM, PaddingMode.None);
```

#### From Base64 or Hex String
```csharp
// From Base64
string base64Key = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
using var aes1 = AesCrypto.CreateFromBase64Key(base64Key);

// From Hex
string hexKey = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
using var aes2 = AesCrypto.CreateFromHexKey(hexKey);
```

#### Generate Random Key
```csharp
// Generate random 256-bit key
using var aes = AesCrypto.CreateRandom(256);
```

### Basic Encryption/Decryption

```csharp
// Prepare data
byte[] plaintext = Encoding.UTF8.GetBytes("Hello, World!");
byte[] key = new byte[32];
RandomNumberGenerator.Fill(key);

using var aes = new AesCrypto(key);

// Generate IV
byte[] iv = aes.GenerateIV();

// Encrypt
byte[] ciphertext = aes.Encrypt(plaintext, iv);

// Decrypt
byte[] decrypted = aes.Decrypt(ciphertext, iv);

// Verify
bool success = plaintext.SequenceEqual(decrypted);
```

### String Encryption Helpers

```csharp
using var aes = new AesCrypto(key);
byte[] iv = aes.GenerateIV();

// Encrypt/Decrypt with Base64
string originalText = "Sensitive data";
string encryptedBase64 = aes.EncryptToBase64(originalText, iv);
string decryptedText = aes.DecryptFromBase64(encryptedBase64, iv);

// Encrypt/Decrypt with Hex
string encryptedHex = aes.EncryptToHex(originalText, iv);
string decryptedFromHex = aes.DecryptFromHex(encryptedHex, iv);
```

## Cipher Modes

### CBC Mode (Default)
```csharp
using var aes = new AesCrypto(key, AesCrypto.ExtendedCipherMode.CBC, PaddingMode.PKCS7);
byte[] iv = aes.GenerateIV();
byte[] ciphertext = aes.Encrypt(data, iv);
```

### GCM Mode (Authenticated Encryption)
```csharp
using var aes = new AesCrypto(key, AesCrypto.ExtendedCipherMode.GCM, PaddingMode.None);
byte[] iv = aes.GenerateNonce(); // 12 bytes for GCM
byte[] associatedData = Encoding.UTF8.GetBytes("AuthData");

// Encrypt (tag is generated automatically)
byte[] ciphertext = aes.Encrypt(data, iv, associatedData);

// To get the tag, you need to use the lower-level methods or EncryptWithPrefixIV
```

### ECB Mode (Legacy)
```csharp
using var aes = new AesCrypto(key, AesCrypto.ExtendedCipherMode.ECB);
byte[] ciphertext = aes.Encrypt(data, new byte[16]); // ECB uses empty IV
```

**Warning**: ECB mode is insecure and should only be used for legacy compatibility.

## Advanced Features

### Stream Processing

```csharp
using var aes = new AesCrypto(key);
byte[] iv = aes.GenerateIV();

// Encrypt stream
using var inputStream = new MemoryStream(data);
using var encryptedStream = new MemoryStream();
await aes.EncryptAsync(inputStream, encryptedStream, iv);

// Decrypt stream
encryptedStream.Position = 0;
using var decryptedStream = new MemoryStream();
await aes.DecryptAsync(encryptedStream, decryptedStream, iv);
```

### Automatic IV Management

```csharp
using var aes = new AesCrypto(key);

// Encrypt with auto-generated IV (IV is prepended to ciphertext)
byte[] combined = aes.EncryptWithPrefixIV(data);
byte[] decrypted = aes.DecryptWithPrefixIV(combined);

// With Base64
string base64Combined = aes.EncryptWithPrefixIVToBase64(data);
byte[] decryptedFromBase64 = aes.DecryptWithPrefixIVFromBase64(base64Combined);
```

### Span-Based Operations (Performance)

```csharp
using var aes = new AesCrypto(key);
byte[] iv = aes.GenerateIV();

// Encrypt with Span
byte[] ciphertext = new byte[data.Length + 16];
aes.Encrypt(data.AsSpan(), iv, ciphertext, out int bytesWritten);

// TryDecrypt with Span
byte[] plaintext = new byte[ciphertext.Length];
bool success = aes.TryDecrypt(ciphertext.AsSpan(), iv, plaintext, out bytesWritten);
```

## GCM Mode Specific Usage

```csharp
byte[] key = new byte[32];
RandomNumberGenerator.Fill(key);

using var aes = new AesCrypto(key, AesCrypto.ExtendedCipherMode.GCM, PaddingMode.None);

// GCM uses 12-byte nonce
byte[] nonce = aes.GenerateNonce();
byte[] associatedData = Encoding.UTF8.GetBytes("Additional authentication data");

// For GCM, use EncryptWithPrefixIV to get the tag
byte[] combined = aes.EncryptWithPrefixIV(data, associatedData);
// combined contains: nonce(16) + ciphertext + tag(16)

// Decrypt with tag verification
byte[] decrypted = aes.DecryptWithPrefixIV(combined, associatedData);
```

## Random Number Generation

```csharp
using var aes = new AesCrypto(key);

// Generate IV (16 bytes for non-GCM, 12 bytes for GCM)
byte[] iv = aes.GenerateIV();
byte[] nonce = aes.GenerateNonce();

// Generate custom length random bytes
byte[] randomBytes = aes.GenerateRandomBytes(64);
```

## Properties and Information

```csharp
using var aes = new AesCrypto(key);

Console.WriteLine($"Algorithm: {aes.AlgorithmName}");
Console.WriteLine($"Key Size: {aes.KeySize} bits");
Console.WriteLine($"Block Size: {aes.BlockSize} bytes");
Console.WriteLine($"Mode: {aes.Mode}");
Console.WriteLine($"Padding: {aes.Padding}");
Console.WriteLine($"Authenticated: {aes.IsAuthenticatedEncryption}");
Console.WriteLine($"Tag Size: {aes.TagSizeInBytes} bytes");
```

## Error Handling

```csharp
try
{
    using var aes = new AesCrypto(key);
    byte[] iv = aes.GenerateIV();
    byte[] ciphertext = aes.Encrypt(data, iv);
    byte[] plaintext = aes.Decrypt(ciphertext, iv);
}
catch (ArgumentException ex)
{
    // Invalid arguments (key, IV, etc.)
}
catch (CryptographicException ex)
{
    // Encryption/decryption failed
}
catch (ObjectDisposedException ex)
{
    // Instance was disposed
}
```

## Thread Safety

`AesCrypto` instances are thread-safe for encryption/decryption operations:

```csharp
using var aes = new AesCrypto(key);
byte[] iv = aes.GenerateIV();

Parallel.For(0, 100, i =>
{
    byte[] data = Encoding.UTF8.GetBytes($"Data {i}");
    byte[] ciphertext = aes.Encrypt(data, iv);
    byte[] plaintext = aes.Decrypt(ciphertext, iv);
    // Thread-safe
});
```

## Resource Management

```csharp
// Always use 'using' statement for proper disposal
using var aes = new AesCrypto(key);

// Manually clear key from memory
aes.ClearKey();

// Dispose releases all resources
aes.Dispose();
```

## Best Practices

1. **Always use authenticated encryption (GCM mode)** when possible
2. **Never reuse IVs** - always generate a new IV for each encryption
3. **Use appropriate key sizes** - 256-bit for maximum security
4. **Store IVs with ciphertext** - use `EncryptWithPrefixIV` methods
5. **Dispose instances properly** - use `using` statements
6. **Validate platform support** for GCM mode if needed:

```csharp
bool gcmSupported = AesCrypto.IsModeSupported(AesCrypto.ExtendedCipherMode.GCM);
```

## Performance Considerations

- **GCM mode** provides both encryption and authentication but requires more CPU
- **Stream operations** are optimized for large files
- **Span-based methods** reduce memory allocations
- **Thread-safe** design enables parallel processing

## See Also

- Test cases in `AesCryptoTests.cs` for comprehensive usage examples
- `ICrypto` interface for alternative implementations
- Official .NET Cryptography documentation