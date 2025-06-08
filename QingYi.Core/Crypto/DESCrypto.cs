using System;
using System.IO;
using System.Security.Cryptography;

namespace QingYi.Core.Crypto
{
    public class DESCrypto : ICrypto
    {
#if NET6_0_OR_GREATER
    private readonly DES _desProvider;
#else
        private readonly DESCryptoServiceProvider _desProvider;
#endif
        private readonly CipherMode _cipherMode;
        private readonly PaddingMode _paddingMode;

        public byte[] Key
        {
            get => _desProvider.Key;
            set => _desProvider.Key = ValidateKey(value, 8);
        }

        public byte[] IV
        {
            get => _desProvider.IV;
            set => _desProvider.IV = ValidateKey(value, 8);
        }

        public DESCrypto() : this(CipherMode.CBC, PaddingMode.PKCS7) { }

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

        public void GenerateKeyIV()
        {
            _desProvider.GenerateKey();
            _desProvider.GenerateIV();
        }

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

        public void Encrypt(Stream input, Stream output)
        {
            using (var encryptor = _desProvider.CreateEncryptor())
            using (var cryptoStream = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
            {
                input.CopyTo(cryptoStream);
                cryptoStream.FlushFinalBlock();
            }
        }

        public void Decrypt(Stream input, Stream output)
        {
            using (var decryptor = _desProvider.CreateDecryptor())
            using (var cryptoStream = new CryptoStream(input, decryptor, CryptoStreamMode.Read))
            {
                cryptoStream.CopyTo(output);
            }
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
    public byte[] Encrypt(ReadOnlySpan<byte> source)
    {
        using (var encryptor = _desProvider.CreateEncryptor())
        {
            return encryptor.TransformFinalBlock(source.ToArray(), 0, source.Length);
        }
    }

    public byte[] Decrypt(ReadOnlySpan<byte> source)
    {
        using (var decryptor = _desProvider.CreateDecryptor())
        {
            return decryptor.TransformFinalBlock(source.ToArray(), 0, source.Length);
        }
    }
#endif

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

    public static class DESCryptoHelper
    {
        // 静态使用方式 - 字节数组
        public static byte[] Encrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                return des.Encrypt(data);
            }
        }

        public static byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                return des.Decrypt(data);
            }
        }

        // 静态使用方式 - 流操作
        public static void Encrypt(Stream input, Stream output, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                des.Encrypt(input, output);
            }
        }

        public static void Decrypt(Stream input, Stream output, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                des.Decrypt(input, output);
            }
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
    public static byte[] Encrypt(ReadOnlySpan<byte> data, byte[] key, byte[] iv)
    {
        using (var des = new DESCrypto())
        {
            des.Key = key;
            des.IV = iv;
            return des.Encrypt(data);
        }
    }

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
