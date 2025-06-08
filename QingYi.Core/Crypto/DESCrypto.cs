using System;
using System.IO;
using System.Security.Cryptography;

namespace QingYi.Core.Crypto
{
    /// <summary>
    /// Provides DES encryption and decryption functionality.
    /// </summary>
    public class DESCrypto : ICrypto
    {
#if NET6_0_OR_GREATER
    private readonly DES _desProvider;
#else
        private readonly DESCryptoServiceProvider _desProvider;
#endif
        private readonly CipherMode _cipherMode;
        private readonly PaddingMode _paddingMode;

        /// <summary>
        /// Gets or sets the symmetric key for DES algorithm. Must be 8 bytes.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when key length is invalid.</exception>
        public byte[] Key
        {
            get => _desProvider.Key;
            set => _desProvider.Key = ValidateKey(value, 8);
        }

        /// <summary>
        /// Gets or sets the initialization vector (IV) for DES algorithm. Must be 8 bytes.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when IV length is invalid.</exception>
        public byte[] IV
        {
            get => _desProvider.IV;
            set => _desProvider.IV = ValidateKey(value, 8);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DESCrypto"/> class 
        /// using CBC mode and PKCS7 padding.
        /// </summary>
        public DESCrypto() : this(CipherMode.CBC, PaddingMode.PKCS7) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DESCrypto"/> class 
        /// with specified cipher mode and padding mode.
        /// </summary>
        /// <param name="cipherMode">The symmetric cipher mode to use.</param>
        /// <param name="paddingMode">The padding mode to use.</param>
        public DESCrypto(CipherMode cipherMode, PaddingMode paddingMode)
        {
#if NET6_0_OR_GREATER && !BROWSER
        // .NET 6+ 使用现代工厂方法
        _desProvider = DES.Create();
#else
            // .NET Standard 2.0 和旧版本
#pragma warning disable SYSLIB0021
            _desProvider = new DESCryptoServiceProvider();
#pragma warning restore SYSLIB0021
#endif
            _desProvider.Mode = cipherMode;
            _desProvider.Padding = paddingMode;
            _cipherMode = cipherMode;
            _paddingMode = paddingMode;
            GenerateKeyIV();
        }

        /// <summary>
        /// Generates a new random key and initialization vector (IV).
        /// </summary>
        public void GenerateKeyIV()
        {
            _desProvider.GenerateKey();
            _desProvider.GenerateIV();
        }

        /// <summary>
        /// Encrypts a byte array using DES.
        /// </summary>
        /// <param name="plainData">The plaintext data to encrypt.</param>
        /// <returns>The encrypted data as a byte array. Returns empty array if input is empty.</returns>
        public byte[] Encrypt(byte[] plainData)
        {
            if (plainData == null || plainData.Length == 0)
                return Array.Empty<byte>();

            using (var encryptor = _desProvider.CreateEncryptor())
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(plainData, 0, plainData.Length);
                    cs.FlushFinalBlock();
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Decrypts a byte array using DES.
        /// </summary>
        /// <param name="encryptedData">The encrypted data to decrypt.</param>
        /// <returns>The decrypted plaintext data. Returns empty array if input is empty.</returns>
        public byte[] Decrypt(byte[] encryptedData)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                return Array.Empty<byte>();

            using (var decryptor = _desProvider.CreateDecryptor())
            using (var ms = new MemoryStream(encryptedData))
            {
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    using (var result = new MemoryStream())
                    {
                        cs.CopyTo(result);
                        return result.ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// Encrypts data from a stream and writes the result to another stream.
        /// </summary>
        /// <param name="input">Stream containing plaintext data.</param>
        /// <param name="output">Stream to write encrypted data to.</param>
        public void Encrypt(Stream input, Stream output)
        {
            using (var encryptor = _desProvider.CreateEncryptor())
            using (var cryptoStream = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
            {
                input.CopyTo(cryptoStream);
                cryptoStream.FlushFinalBlock();
            }
        }

        /// <summary>
        /// Decrypts data from a stream and writes the result to another stream.
        /// </summary>
        /// <param name="input">Stream containing encrypted data.</param>
        /// <param name="output">Stream to write decrypted data to.</param>
        public void Decrypt(Stream input, Stream output)
        {
            using (var decryptor = _desProvider.CreateDecryptor())
            using (var cryptoStream = new CryptoStream(input, decryptor, CryptoStreamMode.Read))
            {
                cryptoStream.CopyTo(output);
            }
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Encrypts data from a read-only span using DES.
        /// </summary>
        /// <param name="source">Read-only span containing plaintext data.</param>
        /// <returns>The encrypted data as a byte array.</returns>
        public byte[] Encrypt(ReadOnlySpan<byte> source)
        {
            using (var encryptor = _desProvider.CreateEncryptor())
            {
                return encryptor.TransformFinalBlock(source.ToArray(), 0, source.Length);
            }
        }

        /// <summary>
        /// Decrypts data from a read-only span using DES.
        /// </summary>
        /// <param name="source">Read-only span containing encrypted data.</param>
        /// <returns>The decrypted plaintext data as a byte array.</returns>
        public byte[] Decrypt(ReadOnlySpan<byte> source)
        {
            using (var decryptor = _desProvider.CreateDecryptor())
            {
                return decryptor.TransformFinalBlock(source.ToArray(), 0, source.Length);
            }
        }
#endif

        /// <summary>
        /// Releases all resources used by the <see cref="DESCrypto"/> object.
        /// </summary>
        public void Dispose()
        {
            _desProvider?.Dispose();
        }

        private byte[] ValidateKey(byte[] key, int requiredLength)
        {
            if (key == null || key.Length != requiredLength)
                throw new ArgumentException($"Key/IV must be {requiredLength} bytes");
            return key;
        }
    }

    /// <summary>
    /// Provides static helper methods for DES encryption and decryption.
    /// </summary>
    public static class DESCryptoHelper
    {
        #region 静态使用方式 - 字节数组
        /// <summary>
        /// Encrypts data using DES with specified key and IV.
        /// </summary>
        /// <param name="data">Plaintext data to encrypt.</param>
        /// <param name="key">DES key (8 bytes).</param>
        /// <param name="iv">DES initialization vector (8 bytes).</param>
        /// <returns>Encrypted data as byte array.</returns>
        public static byte[] Encrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                return des.Encrypt(data);
            }
        }

        /// <summary>
        /// Decrypts data using DES with specified key and IV.
        /// </summary>
        /// <param name="data">Encrypted data to decrypt.</param>
        /// <param name="key">DES key (8 bytes).</param>
        /// <param name="iv">DES initialization vector (8 bytes).</param>
        /// <returns>Decrypted plaintext data as byte array.</returns>
        public static byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                return des.Decrypt(data);
            }
        }
        #endregion

        #region 静态使用方式 - 流操作
        /// <summary>
        /// Encrypts data from a stream and writes to another stream using specified key and IV.
        /// </summary>
        /// <param name="input">Input stream with plaintext data.</param>
        /// <param name="output">Output stream for encrypted data.</param>
        /// <param name="key">DES key (8 bytes).</param>
        /// <param name="iv">DES initialization vector (8 bytes).</param>
        public static void Encrypt(Stream input, Stream output, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                des.Encrypt(input, output);
            }
        }

        /// <summary>
        /// Decrypts data from a stream and writes to another stream using specified key and IV.
        /// </summary>
        /// <param name="input">Input stream with encrypted data.</param>
        /// <param name="output">Output stream for decrypted data.</param>
        /// <param name="key">DES key (8 bytes).</param>
        /// <param name="iv">DES initialization vector (8 bytes).</param>
        public static void Decrypt(Stream input, Stream output, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                des.Decrypt(input, output);
            }
        }
        #endregion

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Encrypts data from a read-only span using specified key and IV.
        /// </summary>
        /// <param name="data">Read-only span containing plaintext data.</param>
        /// <param name="key">DES key (8 bytes).</param>
        /// <param name="iv">DES initialization vector (8 bytes).</param>
        /// <returns>Encrypted data as byte array.</returns>
        public static byte[] Encrypt(ReadOnlySpan<byte> data, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                return des.Encrypt(data);
            }
        }

        /// <summary>
        /// Decrypts data from a read-only span using specified key and IV.
        /// </summary>
        /// <param name="data">Read-only span containing encrypted data.</param>
        /// <param name="key">DES key (8 bytes).</param>
        /// <param name="iv">DES initialization vector (8 bytes).</param>
        /// <returns>Decrypted plaintext data as byte array.</returns>

        public static byte[] Decrypt(ReadOnlySpan<byte> data, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                return des.Decrypt(data);
            }
        }
#endif
    }
}
