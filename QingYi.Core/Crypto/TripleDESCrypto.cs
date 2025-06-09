using System;
using System.IO;
using System.Security.Cryptography;

namespace QingYi.Core.Crypto
{
    /// <summary>
    /// Provides TripleDES encryption and decryption.
    /// </summary>
    public class TripleDESCrypto : ICrypto
    {
#pragma warning disable SYSLIB0021
        private readonly TripleDESCryptoServiceProvider _tripleDesProvider;
#pragma warning disable SYSLIB0021
        private readonly CipherMode _cipherMode;
        private readonly PaddingMode _paddingMode;

        /// <summary>
        /// Gets or sets the symmetric key used for cryptographic operations.
        /// </summary>
        public byte[] Key
        {
            get => _tripleDesProvider.Key;
            set
            {
                // TripleDES key must be 16 or 24 bytes
                if (value != null && value.Length != 16 && value.Length != 24)
                    throw new ArgumentException("Key must be 16 or 24 bytes for TripleDES.");
                _tripleDesProvider.Key = value;
            }
        }

        /// <summary>
        /// Gets or sets the initialization vector (IV) used for cryptographic operations.
        /// </summary>
        public byte[] IV
        {
            get => _tripleDesProvider.IV;
            set
            {
                // IV must be 8 bytes for TripleDES
                if (value != null && value.Length != 8)
                    throw new ArgumentException("IV must be 8 bytes for TripleDES.");
                _tripleDesProvider.IV = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the TripleDESCrypto class with default CBC mode and PKCS7 padding.
        /// </summary>
        public TripleDESCrypto() : this(CipherMode.CBC, PaddingMode.PKCS7) { }

        /// <summary>
        /// Initializes a new instance of the TripleDESCrypto class with specified cipher mode and padding mode.
        /// </summary>
        /// <param name="cipherMode">The cipher mode.</param>
        /// <param name="paddingMode">The padding mode.</param>
        public TripleDESCrypto(CipherMode cipherMode, PaddingMode paddingMode)
        {
            _tripleDesProvider = new TripleDESCryptoServiceProvider
            {
                Mode = cipherMode,
                Padding = paddingMode
            };
            _cipherMode = cipherMode;
            _paddingMode = paddingMode;
            GenerateKeyIV(); // Generate initial key and IV
        }

        /// <summary>
        /// Generates a new random key and initialization vector (IV).
        /// </summary>
        public void GenerateKeyIV()
        {
            _tripleDesProvider.GenerateKey();
            _tripleDesProvider.GenerateIV();
        }

        /// <summary>
        /// Encrypts the specified plaintext data.
        /// </summary>
        /// <param name="plainData">The plaintext data to encrypt.</param>
        /// <returns>The encrypted data as a byte array.</returns>
        public byte[] Encrypt(byte[] plainData)
        {
            if (plainData == null || plainData.Length == 0)
                return Array.Empty<byte>();

            using (var encryptor = _tripleDesProvider.CreateEncryptor())
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
        /// Decrypts the specified encrypted data.
        /// </summary>
        /// <param name="encryptedData">The encrypted data to decrypt.</param>
        /// <returns>The decrypted plaintext data as a byte array.</returns>
        public byte[] Decrypt(byte[] encryptedData)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                return Array.Empty<byte>();

            using (var decryptor = _tripleDesProvider.CreateDecryptor())
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
        /// Encrypts data from the input stream and writes the result to the output stream.
        /// </summary>
        /// <param name="input">The stream containing plaintext data.</param>
        /// <param name="output">The stream to write encrypted data to.</param>
        public void Encrypt(Stream input, Stream output)
        {
            using (var encryptor = _tripleDesProvider.CreateEncryptor())
            using (var cryptoStream = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
            {
                input.CopyTo(cryptoStream);
                cryptoStream.FlushFinalBlock();
            }
        }

        /// <summary>
        /// Decrypts data from the input stream and writes the result to the output stream.
        /// </summary>
        /// <param name="input">The stream containing encrypted data.</param>
        /// <param name="output">The stream to write decrypted data to.</param>
        public void Decrypt(Stream input, Stream output)
        {
            using (var decryptor = _tripleDesProvider.CreateDecryptor())
            using (var cryptoStream = new CryptoStream(input, decryptor, CryptoStreamMode.Read))
            {
                cryptoStream.CopyTo(output);
            }
        }

        /// <summary>
        /// Releases all resources used by the TripleDESCrypto.
        /// </summary>
        public void Dispose()
        {
            _tripleDesProvider?.Dispose();
        }

        // Conditionally compiled methods for Span support
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        /// <summary>
        /// Encrypts data from a read-only span buffer.
        /// </summary>
        /// <param name="source">The read-only span containing plaintext data.</param>
        /// <returns>The encrypted data as a byte array.</returns>
        public byte[] Encrypt(ReadOnlySpan<byte> source)
        {
            if (source.IsEmpty)
                return Array.Empty<byte>();

            using (var encryptor = _tripleDesProvider.CreateEncryptor())
            {
                return encryptor.TransformFinalBlock(source.ToArray(), 0, source.Length);
            }
        }

        /// <summary>
        /// Decrypts data from a read-only span buffer.
        /// </summary>
        /// <param name="source">The read-only span containing encrypted data.</param>
        /// <returns>The decrypted plaintext data as a byte array.</returns>
        public byte[] Decrypt(ReadOnlySpan<byte> source)
        {
            if (source.IsEmpty)
                return Array.Empty<byte>();

            using (var decryptor = _tripleDesProvider.CreateDecryptor())
            {
                return decryptor.TransformFinalBlock(source.ToArray(), 0, source.Length);
            }
        }
#endif
    }

    /// <summary>
    /// Provides static methods for TripleDES encryption and decryption.
    /// </summary>
    public static class TripleDESHelper
    {
        /// <summary>
        /// Encrypts the specified plaintext data using TripleDES.
        /// </summary>
        /// <param name="data">The plaintext data to encrypt.</param>
        /// <param name="key">The symmetric key (16 or 24 bytes).</param>
        /// <param name="iv">The initialization vector (8 bytes).</param>
        /// <returns>The encrypted data as a byte array.</returns>
        public static byte[] Encrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var des = new TripleDESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                return des.Encrypt(data);
            }
        }

        /// <summary>
        /// Decrypts the specified encrypted data using TripleDES.
        /// </summary>
        /// <param name="data">The encrypted data to decrypt.</param>
        /// <param name="key">The symmetric key (16 or 24 bytes).</param>
        /// <param name="iv">The initialization vector (8 bytes).</param>
        /// <returns>The decrypted plaintext data as a byte array.</returns>
        public static byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var des = new TripleDESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                return des.Decrypt(data);
            }
        }

        /// <summary>
        /// Encrypts data from the input stream and writes the result to the output stream using TripleDES.
        /// </summary>
        /// <param name="input">The stream containing plaintext data.</param>
        /// <param name="output">The stream to write encrypted data to.</param>
        /// <param name="key">The symmetric key (16 or 24 bytes).</param>
        /// <param name="iv">The initialization vector (8 bytes).</param>
        public static void Encrypt(Stream input, Stream output, byte[] key, byte[] iv)
        {
            using (var des = new TripleDESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                des.Encrypt(input, output);
            }
        }

        /// <summary>
        /// Decrypts data from the input stream and writes the result to the output stream using TripleDES.
        /// </summary>
        /// <param name="input">The stream containing encrypted data.</param>
        /// <param name="output">The stream to write decrypted data to.</param>
        /// <param name="key">The symmetric key (16 or 24 bytes).</param>
        /// <param name="iv">The initialization vector (8 bytes).</param>
        public static void Decrypt(Stream input, Stream output, byte[] key, byte[] iv)
        {
            using (var des = new TripleDESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                des.Decrypt(input, output);
            }
        }

        // Conditionally compiled methods for Span support
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        /// <summary>
        /// Encrypts data from a read-only span buffer using TripleDES.
        /// </summary>
        /// <param name="data">The read-only span containing plaintext data.</param>
        /// <param name="key">The symmetric key (16 or 24 bytes).</param>
        /// <param name="iv">The initialization vector (8 bytes).</param>
        /// <returns>The encrypted data as a byte array.</returns>
        public static byte[] Encrypt(ReadOnlySpan<byte> data, byte[] key, byte[] iv)
        {
            using (var des = new TripleDESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                return des.Encrypt(data);
            }
        }

        /// <summary>
        /// Decrypts data from a read-only span buffer using TripleDES.
        /// </summary>
        /// <param name="data">The read-only span containing encrypted data.</param>
        /// <param name="key">The symmetric key (16 or 24 bytes).</param>
        /// <param name="iv">The initialization vector (8 bytes).</param>
        /// <returns>The decrypted plaintext data as a byte array.</returns>
        public static byte[] Decrypt(ReadOnlySpan<byte> data, byte[] key, byte[] iv)
        {
            using (var des = new TripleDESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                return des.Decrypt(data);
            }
        }
#endif
    }
}