using System;
using System.IO;
using System.Security.Cryptography;

namespace QingYi.Core.Crypto
{
    public class DESCrypto : ICrypto
    {
        private readonly DESCryptoServiceProvider _desProvider;
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
            _desProvider = new DESCryptoServiceProvider
            {
                Mode = cipherMode,
                Padding = paddingMode
            };
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
                // 使用兼容.NET Standard 2.0的CryptoStream构造函数
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
            {
                // 兼容.NET Standard 2.0的流处理方式
                using (var cryptoStream = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
                {
                    input.CopyTo(cryptoStream);
                }

                // 确保所有数据都刷新到输出流
                output.Flush();
            }
        }

        public void Decrypt(Stream input, Stream output)
        {
            using (var decryptor = _desProvider.CreateDecryptor())
            {
                // 兼容.NET Standard 2.0的流处理方式
                using (var cryptoStream = new CryptoStream(input, decryptor, CryptoStreamMode.Read))
                {
                    cryptoStream.CopyTo(output);
                }

                // 确保所有数据都刷新到输出流
                output.Flush();
            }
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public byte[] Encrypt(ReadOnlySpan<byte> source)
        {
            using (var encryptor = _desProvider.CreateEncryptor())
            {
                // 使用TransformFinalBlock处理整个数据块
                return encryptor.TransformFinalBlock(source.ToArray(), 0, source.Length);
            }
        }

        public byte[] Decrypt(ReadOnlySpan<byte> source)
        {
            using (var decryptor = _desProvider.CreateDecryptor())
            {
                // 使用TransformFinalBlock处理整个数据块
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

    public static class CryptoHelper
    {
        // 静态使用方式 - 字节数组
        public static byte[] DESEncrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                return des.Encrypt(data);
            }
        }

        public static byte[] DESDecrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                return des.Decrypt(data);
            }
        }

        // 静态使用方式 - 流操作
        public static void DESEncrypt(Stream input, Stream output, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                des.Encrypt(input, output);
            }
        }

        public static void DESDecrypt(Stream input, Stream output, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                des.Decrypt(input, output);
            }
        }

        // 条件编译支持Span
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public static byte[] DESEncrypt(ReadOnlySpan<byte> data, byte[] key, byte[] iv)
        {
            using (var des = new DESCrypto())
            {
                des.Key = key;
                des.IV = iv;
                return des.Encrypt(data);
            }
        }

        public static byte[] DESDecrypt(ReadOnlySpan<byte> data, byte[] key, byte[] iv)
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
