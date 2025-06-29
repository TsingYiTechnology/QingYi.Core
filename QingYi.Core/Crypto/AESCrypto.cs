#if !BROWSER
using System;
using System.IO;
using System.Security.Cryptography;

namespace QingYi.Core.Crypto
{
    public class AESCrypto : ICrypto
    {
#if NET6_0_OR_GREATER
        private readonly Aes _aesProvider = Aes.Create();
#else
    private readonly AesManaged _aesProvider = new AesManaged();
#endif

        public byte[] Key
        {
            get => _aesProvider.Key;
            set
            {
                // 验证AES密钥长度（128/192/256位）
                if (value == null || (value.Length != 16 && value.Length != 24 && value.Length != 32))
                    throw new ArgumentException("AES key must be 16, 24 or 32 bytes (128, 192, 256 bits)");
                _aesProvider.Key = value;
            }
        }

        public byte[] IV
        {
            get => _aesProvider.IV;
            set
            {
                // 验证IV长度（16字节）
                if (value == null || value.Length != 16)
                    throw new ArgumentException("IV must be 16 bytes");
                _aesProvider.IV = value;
            }
        }

        public AESCrypto() : this(CipherMode.CBC, PaddingMode.PKCS7) { }

        public AESCrypto(CipherMode cipherMode, PaddingMode paddingMode)
        {
            _aesProvider.Mode = cipherMode;
            _aesProvider.Padding = paddingMode;
            GenerateKeyIV();
        }

        public void GenerateKeyIV()
        {
            _aesProvider.GenerateKey();
            _aesProvider.GenerateIV();
        }

        public byte[] Encrypt(byte[] plainData)
        {
            if (plainData == null || plainData.Length == 0)
                return Array.Empty<byte>();

            using (var encryptor = _aesProvider.CreateEncryptor())
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

        public byte[] Decrypt(byte[] encryptedData)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                return Array.Empty<byte>();

            using (var decryptor = _aesProvider.CreateDecryptor())
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

        public void Encrypt(Stream input, Stream output)
        {
            using (var encryptor = _aesProvider.CreateEncryptor())
            using (var cryptoStream = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
            {
                input.CopyTo(cryptoStream);
                cryptoStream.FlushFinalBlock();
            }
        }

        public void Decrypt(Stream input, Stream output)
        {
            using (var decryptor = _aesProvider.CreateDecryptor())
            using (var cryptoStream = new CryptoStream(input, decryptor, CryptoStreamMode.Read))
            {
                cryptoStream.CopyTo(output);
            }
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        public byte[] Encrypt(ReadOnlySpan<byte> source)
        {
            using (var encryptor = _aesProvider.CreateEncryptor())
            {
                return encryptor.TransformFinalBlock(source.ToArray(), 0, source.Length);
            }
        }

        public byte[] Decrypt(ReadOnlySpan<byte> source)
        {
            using (var decryptor = _aesProvider.CreateDecryptor())
            {
                return decryptor.TransformFinalBlock(source.ToArray(), 0, source.Length);
            }
        }
#endif

        public void Dispose()
        {
            _aesProvider?.Dispose();
        }
    }

    public static class AESCryptoHelper
    {
        #region AES静态使用方式 - 字节数组
        public static byte[] Encrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var aes = new AESCrypto())
            {
                aes.Key = key;
                aes.IV = iv;
                return aes.Encrypt(data);
            }
        }

        public static byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var aes = new AESCrypto())
            {
                aes.Key = key;
                aes.IV = iv;
                return aes.Decrypt(data);
            }
        }
        #endregion

        #region AES静态使用方式 - 流操作
        public static void Encrypt(Stream input, Stream output, byte[] key, byte[] iv)
        {
            using (var aes = new AESCrypto())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Encrypt(input, output);
            }
        }

        public static void Decrypt(Stream input, Stream output, byte[] key, byte[] iv)
        {
            using (var aes = new AESCrypto())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Decrypt(input, output);
            }
        }
        #endregion

        #region Span
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        public static byte[] Encrypt(ReadOnlySpan<byte> data, byte[] key, byte[] iv)
        {
            using (var aes = new AESCrypto())
            {
                aes.Key = key;
                aes.IV = iv;
                return aes.Encrypt(data);
            }
        }

        public static byte[] Decrypt(ReadOnlySpan<byte> data, byte[] key, byte[] iv)
        {
            using (var aes = new AESCrypto())
            {
                aes.Key = key;
                aes.IV = iv;
                return aes.Decrypt(data);
            }
        }
#endif
        #endregion
    }
}
#endif
