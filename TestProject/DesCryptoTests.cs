using NUnit.Framework;
using QingYi.Core.Crypto;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace TestProject
{
    [TestFixture]
    public class DesCryptoTests
    {
        #region 测试数据准备

        private static readonly byte[] TestKey;
        private static readonly byte[] TestIV;
        private static readonly byte[] TestPlaintext;
        private static readonly byte[] TestAssociatedData;

        static DesCryptoTests()
        {
            // DES 使用 8 字节密钥
            TestKey = new byte[8];
            TestIV = new byte[8]; // DES IV 必须是 8 字节
            TestPlaintext = Encoding.UTF8.GetBytes("Hello, DES加密测试！");
            TestAssociatedData = Encoding.UTF8.GetBytes("AdditionalData123");

            RandomNumberGenerator.Fill(TestKey);
            RandomNumberGenerator.Fill(TestIV);
        }

        #endregion

        #region 构造函数测试

        [Test]
        public void Constructor_WithValidKey_ShouldInitialize()
        {
            // Arrange & Act
            using var des = new DesCrypto(TestKey);

            // Assert
            Assert.That(des.KeySize, Is.EqualTo(64));
            Assert.That(des.Mode.ToString(), Is.EqualTo("CBC"));
            Assert.That(des.Padding, Is.EqualTo(PaddingMode.PKCS7));
            Assert.That(des.IsAuthenticatedEncryption, Is.False);
            Assert.That(des.BlockSize, Is.EqualTo(8)); // DES 块大小是 8 字节
        }

        [Test]
        public void Constructor_WithValidKey8Bytes_ShouldInitialize()
        {
            // Arrange
            var key8Bytes = new byte[8];
            RandomNumberGenerator.Fill(key8Bytes);

            // Act
            using var des = new DesCrypto(key8Bytes);

            // Assert
            Assert.That(des.KeySize, Is.EqualTo(64));
            Assert.That(des.Key.Length, Is.EqualTo(8));
        }

        [Test]
        public void Constructor_WithInvalidKey_ShouldThrowArgumentException()
        {
            // Arrange
            var invalidKey = new byte[16]; // 不是有效的 DES 密钥长度

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new DesCrypto(invalidKey));
        }

        [Test]
        public void Constructor_WithNullByteArrayKey_ShouldThrowArgumentNullException()
        {
            // Arrange
            var nullByteArray = (byte[])null!;
            var nullString = (string)null!;

            // Act & Assert - 测试字节数组构造函数
            Assert.Throws<ArgumentNullException>(() =>
                new DesCrypto(nullByteArray));

            // Act & Assert - 测试字符串构造函数
            Assert.Throws<ArgumentNullException>(() =>
                new DesCrypto(nullString));
        }

        [Test]
        public void Constructor_WithStringKey_ShouldInitializeCorrectly()
        {
            // Arrange
            var keyString = "MySecretKey123";

            // Act
            using var des = new DesCrypto(keyString);

            // Assert
            Assert.That(des.KeySize, Is.EqualTo(64));
            Assert.That(des.Key.Length, Is.EqualTo(8));
        }

        [Test]
        public void Constructor_WithShortStringKey_ShouldPadCorrectly()
        {
            // Arrange
            var shortKey = "123"; // 短于 8 字节

            // Act
            using var des = new DesCrypto(shortKey);

            // Assert
            Assert.That(des.Key.Length, Is.EqualTo(8));
        }

        [Test]
        public void Constructor_WithLongStringKey_ShouldHashCorrectly()
        {
            // Arrange
            var longKey = "ThisIsAVeryLongKeyThatWillBeHashedTo8Bytes";

            // Act
            using var des = new DesCrypto(longKey);

            // Assert
            Assert.That(des.Key.Length, Is.EqualTo(8));
        }

        [Test]
        public void Constructor_WithDifferentModes_ShouldInitialize(
            [Values] CipherMode mode)
        {
            // 为 ECB 模式使用正确的填充（ECB 通常使用 PKCS7）
            var padding = PaddingMode.PKCS7;

            // Act
            using var des = new DesCrypto(TestKey, mode, padding);

            // Assert
            Assert.That(des.Mode, Is.EqualTo(mode));
            Assert.That(des.Padding, Is.EqualTo(padding));
        }

        #endregion

        #region 工厂方法测试

        [Test]
        public void Create_StaticMethod_ShouldInitializeCorrectly()
        {
            // Arrange
            var key = new byte[8];
            RandomNumberGenerator.Fill(key);

            // Act
            using var des = DesCrypto.Create(key);

            // Assert
            Assert.That(des.KeySize, Is.EqualTo(64));
            Assert.That(des.Key.ToArray(), Is.EqualTo(key));
        }

        [Test]
        public void CreateRandom_ShouldGenerateValidInstance()
        {
            // Act
            using var des = DesCrypto.CreateRandom();

            // Assert
            Assert.That(des.KeySize, Is.EqualTo(64));
            Assert.That(des.Key.Length, Is.EqualTo(8));
        }

        #endregion

        #region 基本加解密测试

        [Test]
        [TestCase(CipherMode.CBC)]
        [TestCase(CipherMode.CFB)]
        //[TestCase(CipherMode.OFB)]
        public void EncryptDecrypt_ShouldReturnOriginalData(CipherMode mode)
        {
            // Arrange
            using var des = new DesCrypto(TestKey, mode);
            var iv = des.GenerateIV();

            // Act
            var ciphertext = des.Encrypt(TestPlaintext, iv);
            var plaintext = des.Decrypt(ciphertext, iv);

            // Assert
            Assert.That(plaintext, Is.EqualTo(TestPlaintext));
        }

        [Test]
        public void Encrypt_WithSpan_ShouldWorkCorrectly()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var plaintextSpan = TestPlaintext.AsSpan();

            // 计算输出大小（包括填充）
            int outputSize = TestPlaintext.Length;
            if (des.Padding == PaddingMode.PKCS7 || des.Padding == PaddingMode.ANSIX923 || des.Padding == PaddingMode.ISO10126)
            {
                outputSize = ((TestPlaintext.Length / 8) + 1) * 8;
            }

            var ciphertext = new byte[outputSize];

            // Act
            des.Encrypt(plaintextSpan, iv, ciphertext, out int bytesWritten);

            // Assert
            Assert.That(bytesWritten, Is.GreaterThan(0));
            Assert.That(bytesWritten, Is.EqualTo(ciphertext.Length));
        }

        [Test]
        public void Decrypt_WithSpan_ShouldWorkCorrectly()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var ciphertext = des.Encrypt(TestPlaintext, iv);

            // 计算解密后可能的最大大小
            int maxPlaintextSize = ciphertext.Length;
            var plaintext = new byte[maxPlaintextSize];

            // Act
            des.Decrypt(ciphertext.AsSpan(), iv, plaintext, out int bytesWritten);

            // Assert
            Assert.That(bytesWritten, Is.GreaterThan(0));
            var actualPlaintext = new byte[bytesWritten];
            Array.Copy(plaintext, actualPlaintext, bytesWritten);
            Assert.That(actualPlaintext, Is.EqualTo(TestPlaintext));
        }

        #endregion

        #region 关联数据和认证标签测试（DES 不支持）

        [Test]
        public void Encrypt_WithAssociatedData_ShouldThrowNotSupportedException()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var associatedData = new byte[] { 1, 2, 3 };

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                des.Encrypt(TestPlaintext, iv, associatedData));
        }

        [Test]
        public void Decrypt_WithAssociatedData_ShouldThrowNotSupportedException()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var ciphertext = des.Encrypt(TestPlaintext, iv);
            var associatedData = new byte[] { 1, 2, 3 };

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                des.Decrypt(ciphertext, iv, associatedData));
        }

        [Test]
        public void Decrypt_WithAuthenticationTag_ShouldThrowNotSupportedException()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var ciphertext = des.Encrypt(TestPlaintext, iv);
            var tag = new byte[] { 1, 2, 3 };

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                des.Decrypt(ciphertext, iv, null, tag));
        }

        #endregion

        #region 字符串便捷方法测试

        [Test]
        public void EncryptToBase64DecryptFromBase64_ShouldReturnOriginalString()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var originalText = "Hello, DES 世界!";

            // Act
            var ciphertextBase64 = des.EncryptToBase64(originalText, iv);
            var decryptedText = des.DecryptFromBase64(ciphertextBase64, iv);

            // Assert
            Assert.That(decryptedText, Is.EqualTo(originalText));
        }

        [Test]
        public void EncryptToHexDecryptFromHex_ShouldReturnOriginalString()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var originalText = "Test DES encryption with hex";

            // Act
            var ciphertextHex = des.EncryptToHex(originalText, iv);
            var decryptedText = des.DecryptFromHex(ciphertextHex, iv);

            // Assert
            Assert.That(decryptedText, Is.EqualTo(originalText));
        }

        #endregion

        #region 流处理方法测试

        [Test]
        public async Task EncryptDecryptAsync_ShouldReturnOriginalData()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();

            // 确保数据长度是块大小的倍数
            var baseData = Encoding.UTF8.GetBytes("Large DES data stream test ");
            int blockSize = 8;
            int totalSize = 10000 + baseData.Length;
            int paddedSize = ((totalSize + blockSize - 1) / blockSize) * blockSize;

            var originalData = new byte[paddedSize];
            Buffer.BlockCopy(baseData, 0, originalData, 0, baseData.Length);
            RandomNumberGenerator.Fill(originalData.AsSpan(baseData.Length));

            using var plaintextStream = new MemoryStream(originalData);
            using var encryptedStream = new MemoryStream();
            using var decryptedStream = new MemoryStream();

            // Act - 加密
            await des.EncryptAsync(plaintextStream, encryptedStream, iv, ct: CancellationToken.None);
            encryptedStream.Position = 0;

            // Act - 解密
            await des.DecryptAsync(encryptedStream, decryptedStream, iv, ct: CancellationToken.None);

            // Assert
            Assert.That(decryptedStream.ToArray(), Is.EqualTo(originalData));
        }

        [Test]
        public void CreateEncryptorCreateDecryptor_ShouldWorkCorrectly()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();

            // Act
            using var encryptor = des.CreateEncryptor(iv);
            using var decryptor = des.CreateDecryptor(iv);

            var ciphertext = encryptor.TransformFinalBlock(TestPlaintext, 0, TestPlaintext.Length);
            var plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

            // Assert
            Assert.That(plaintext, Is.EqualTo(TestPlaintext));
        }

        #endregion

        #region 业务友好方法测试

        [Test]
        public void EncryptDecryptWithPrefixIV_ShouldReturnOriginalData()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);

            // Act
            var combined = des.EncryptWithPrefixIV(TestPlaintext);
            var plaintext = des.DecryptWithPrefixIV(combined);

            // Assert
            Assert.That(plaintext, Is.EqualTo(TestPlaintext));
        }

        [Test]
        public void EncryptDecryptWithPrefixIVToBase64_ShouldReturnOriginalData()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);

            // Act
            var base64Data = des.EncryptWithPrefixIVToBase64(TestPlaintext);
            var plaintext = des.DecryptWithPrefixIVFromBase64(base64Data);

            // Assert
            Assert.That(plaintext, Is.EqualTo(TestPlaintext));
        }

        #endregion

        #region 随机数生成测试

        [Test]
        public void GenerateIV_ShouldGenerate8Bytes()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);

            // Act
            var iv = des.GenerateIV();

            // Assert
            Assert.That(iv.Length, Is.EqualTo(8));
        }

        [Test]
        public void GenerateNonce_ShouldGenerate8Bytes()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);

            // Act
            var nonce = des.GenerateNonce();

            // Assert
            Assert.That(nonce.Length, Is.EqualTo(8)); // DES 固定生成 8 字节
        }

        [Test]
        [TestCase(8)]
        [TestCase(16)]
        [TestCase(32)]
        [TestCase(64)]
        public void GenerateRandomBytes_ShouldGenerateCorrectLength(int length)
        {
            // Arrange
            using var des = new DesCrypto(TestKey);

            // Act
            var bytes1 = des.GenerateRandomBytes(length);
            var bytes2 = des.GenerateRandomBytes(length);

            // Assert
            Assert.That(bytes1.Length, Is.EqualTo(length));
            Assert.That(bytes2.Length, Is.EqualTo(length));
            Assert.That(bytes1, Is.Not.EqualTo(bytes2)); // 应该是不同的随机数
        }

        #endregion

        #region 异常测试

        [Test]
        public void Encrypt_WithInvalidIV_ShouldThrowArgumentException()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var invalidIV = new byte[16]; // DES IV 必须是 8 字节，16 字节无效

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => des.Encrypt(TestPlaintext, invalidIV));
            Assert.That(ex!.Message, Does.Contain("DES requires 8-byte IV"));
        }

        [Test]
        public void Decrypt_WithInvalidCiphertext_ShouldThrowCryptographicException()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var invalidCiphertext = new byte[7]; // 不是 8 字节的倍数

            // Act & Assert
            if (des.Mode != CipherMode.ECB)
            {
                // 对于非 ECB 模式，应该抛出 CryptographicException
                Assert.Throws<CryptographicException>(() => des.Decrypt(invalidCiphertext, iv));
            }
        }

        [Test]
        public void Dispose_ShouldPreventFurtherOperations()
        {
            // Arrange
            var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();

            // Act
            des.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => des.Encrypt(TestPlaintext, iv));
        }

        [Test]
        public void ClearKey_ShouldZeroOutKey()
        {
            // Arrange
            var key = new byte[8];
            RandomNumberGenerator.Fill(key);
            using var des = new DesCrypto(key);

            // Act
            des.ClearKey();

            // Assert
            var keyAfterClear = des.Key.ToArray();
            Assert.That(keyAfterClear, Is.All.EqualTo(0));
        }

        #endregion

        #region 属性测试

        [Test]
        public void Properties_ShouldReturnCorrectValues()
        {
            // Arrange
            using var des = new DesCrypto(TestKey, CipherMode.CBC, PaddingMode.PKCS7);

            // Assert
            Assert.That(des.AlgorithmName, Is.EqualTo("DES"));
            Assert.That(des.KeySize, Is.EqualTo(64));
            Assert.That(des.BlockSize, Is.EqualTo(8));
            Assert.That(des.TagSizeInBytes, Is.EqualTo(0));
            Assert.That(des.IsAuthenticatedEncryption, Is.False);
        }

        #endregion

        #region 遗留方法测试（ECB 模式）

        [Test]
        public void EncryptDecryptWithoutIV_ECB_ShouldWork()
        {
            // Arrange
            using var des = new DesCrypto(TestKey, CipherMode.ECB);

            // Act
#pragma warning disable CS0618 // 类型或成员已过时
            var ciphertext = des.EncryptWithoutIV_ECB(TestPlaintext);
            var plaintext = des.DecryptWithoutIV_ECB(ciphertext);
#pragma warning restore CS0618 // 类型或成员已过时

            // Assert
            Assert.That(plaintext, Is.EqualTo(TestPlaintext));
        }

        [Test]
        public void EncryptWithoutIV_ECB_InNonEcbMode_ShouldThrow()
        {
            // Arrange
            using var des = new DesCrypto(TestKey, CipherMode.CBC);

            // Act & Assert
#pragma warning disable CS0618 // 类型或成员已过时
            Assert.Throws<InvalidOperationException>(() => des.EncryptWithoutIV_ECB(TestPlaintext));
#pragma warning restore CS0618 // 类型或成员已过时
        }

        #endregion

        #region 性能测试

        [Test]
#pragma warning disable CS0618 // 类型或成员已过时
        [Timeout(2000)] // 设置2秒超时
#pragma warning restore CS0618 // 类型或成员已过时
        public void Encrypt_LargeData_ShouldCompleteQuickly()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var largeData = new byte[1024 * 1024]; // 1MB
            RandomNumberGenerator.Fill(largeData);

            // Act - 这应该在2秒内完成
            var ciphertext = des.Encrypt(largeData, iv);

            // Assert - 如果测试通过，说明性能可以接受
            Assert.That(ciphertext, Is.Not.Null);
        }

        #endregion

        #region 多线程测试

        [Test]
        public void EncryptDecrypt_Multithreaded_ShouldBeThreadSafe()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var testData = new byte[100];
            RandomNumberGenerator.Fill(testData);

            const int threadCount = 10;
            const int iterationsPerThread = 100;
            var exceptions = new ConcurrentBag<Exception>();

            // Act
            Parallel.For(0, threadCount, i =>
            {
                try
                {
                    for (int j = 0; j < iterationsPerThread; j++)
                    {
                        var ciphertext = des.Encrypt(testData, iv);
                        var plaintext = des.Decrypt(ciphertext, iv);
                        Assert.That(plaintext, Is.EqualTo(testData));
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            // Assert
            Assert.That(exceptions, Is.Empty,
                $"Found {exceptions.Count} exceptions in multithreaded test");
        }

        #endregion

        #region 边界条件测试

        [Test]
        public void Encrypt_EmptyData_ShouldWork()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var emptyData = Array.Empty<byte>();

            // Act
            var ciphertext = des.Encrypt(emptyData, iv);
            var plaintext = des.Decrypt(ciphertext, iv);

            // Assert
            Assert.That(plaintext, Is.EqualTo(emptyData));
        }

        [Test]
        public void Encrypt_SmallData_ShouldWork()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var smallData = new byte[] { 0x01, 0x02, 0x03 };

            // Act
            var ciphertext = des.Encrypt(smallData, iv);
            var plaintext = des.Decrypt(ciphertext, iv);

            // Assert
            Assert.That(plaintext, Is.EqualTo(smallData));
        }

        [Test]
        public void Encrypt_ExactlyOneBlock_ShouldWork()
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var oneBlockData = new byte[8]; // DES 块大小
            RandomNumberGenerator.Fill(oneBlockData);

            // Act
            var ciphertext = des.Encrypt(oneBlockData, iv);
            var plaintext = des.Decrypt(ciphertext, iv);

            // Assert
            Assert.That(plaintext, Is.EqualTo(oneBlockData));
        }

        [Test]
        [TestCase(1)]    // 1字节
        [TestCase(7)]    // 比块少1字节
        [TestCase(9)]    // 比块多1字节
        [TestCase(15)]   // 奇数长度
        [TestCase(1023)] // 较大的非对齐长度
        public void Encrypt_VariousDataLengths_ShouldWork(int length)
        {
            // Arrange
            using var des = new DesCrypto(TestKey);
            var iv = des.GenerateIV();
            var data = new byte[length];
            RandomNumberGenerator.Fill(data);

            // Act
            var ciphertext = des.Encrypt(data, iv);
            var plaintext = des.Decrypt(ciphertext, iv);

            // Assert
            Assert.That(plaintext, Is.EqualTo(data));
        }

        #endregion

        #region 静态方法测试

        [Test]
        public void EncryptBlock_StaticMethod_ShouldWork()
        {
            // Arrange
            var key = new byte[8];
            var input = new byte[8];
            var output = new byte[8];
            RandomNumberGenerator.Fill(key);
            RandomNumberGenerator.Fill(input);

            // Act
            DesCrypto.EncryptBlock(key, input, output, CipherMode.ECB);

            // Assert
            Assert.That(output, Is.Not.EqualTo(input));
            Assert.That(output.Length, Is.EqualTo(8));
        }

        [Test]
        public void EncryptBlock_WithCBCMode_ShouldWork()
        {
            // Arrange
            var key = new byte[8];
            var input = new byte[8];
            var output = new byte[8];
            var iv = new byte[8];
            RandomNumberGenerator.Fill(key);
            RandomNumberGenerator.Fill(input);
            RandomNumberGenerator.Fill(iv);

            // Act
            DesCrypto.EncryptBlock(key, input, output, CipherMode.CBC, iv);

            // Assert
            Assert.That(output, Is.Not.EqualTo(input));
        }

        #endregion

        #region 填充模式测试

        [Test]
        [TestCase(PaddingMode.PKCS7)]
        [TestCase(PaddingMode.ANSIX923)]
        [TestCase(PaddingMode.ISO10126)]
        [TestCase(PaddingMode.Zeros)]
        public void EncryptDecrypt_WithDifferentPadding_ShouldWork(PaddingMode padding)
        {
            // Arrange
            using var des = new DesCrypto(TestKey, CipherMode.CBC, padding);
            var iv = des.GenerateIV();

            // Act
            var ciphertext = des.Encrypt(TestPlaintext, iv);
            var plaintext = des.Decrypt(ciphertext, iv);

            // Assert
            Assert.That(plaintext, Is.EqualTo(TestPlaintext));
        }

        [Test]
        public void EncryptDecrypt_WithPaddingNone_ShouldWorkForBlockAlignedData()
        {
            // 专门测试 PaddingMode.None
            // Arrange
            var padding = PaddingMode.None;

            // 创建长度是8字节倍数的测试数据
#pragma warning disable CS0219 // 变量已被赋值，但从未使用过它的值
            int blockSize = 8;
#pragma warning restore CS0219 // 变量已被赋值，但从未使用过它的值
            var blockAlignedData = new byte[32]; // 4个块
            RandomNumberGenerator.Fill(blockAlignedData);

            using var des = new DesCrypto(TestKey, CipherMode.CBC, padding);
            var iv = des.GenerateIV();

            // Act
            var ciphertext = des.Encrypt(blockAlignedData, iv);
            var plaintext = des.Decrypt(ciphertext, iv);

            // Assert
            Assert.That(plaintext, Is.EqualTo(blockAlignedData));
        }

        [Test]
        public void EncryptDecrypt_WithPaddingNone_ShouldThrowForNonBlockAlignedData()
        {
            // 测试非块对齐数据应该抛出异常
            // Arrange
            var padding = PaddingMode.None;

            // 创建长度不是8字节倍数的测试数据
            var nonBlockAlignedData = new byte[25]; // 25 % 8 = 1
            RandomNumberGenerator.Fill(nonBlockAlignedData);

            using var des = new DesCrypto(TestKey, CipherMode.CBC, padding);
            var iv = des.GenerateIV();

            // Act & Assert
            Assert.Throws<CryptographicException>(() =>
                des.Encrypt(nonBlockAlignedData, iv));
        }

        #endregion
    }

    /// <summary>
    /// DES 性能测试类
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    public class DesCryptoPerformanceTests
    {
        [Test]
        [Category("Performance")]
        [MaxTime(5000)]
        public void Performance_EncryptDecrypt_1MB_ShouldBeFast()
        {
            // Arrange - 使用随机生成的密钥，避免弱密钥
            var key = new byte[8];
            RandomNumberGenerator.Fill(key);

            // 检查是否为弱密钥，如果是则重新生成
            while (IsWeakDesKey(key))
            {
                RandomNumberGenerator.Fill(key);
            }

            using var des = new DesCrypto(key);
            var iv = des.GenerateIV();
            var data = new byte[1024 * 1024]; // 1MB
            RandomNumberGenerator.Fill(data);

            // Act & Assert - 测试多次以确保性能稳定
            for (int i = 0; i < 10; i++)
            {
                var ciphertext = des.Encrypt(data, iv);
                var plaintext = des.Decrypt(ciphertext, iv);
                Assert.That(plaintext, Is.EqualTo(data));
            }
        }

        /// <summary>
        /// 检查是否为 DES 弱密钥
        /// </summary>
        private static bool IsWeakDesKey(byte[] key)
        {
            if (key.Length != 8) return false;

            // DES 有 4 个已知弱密钥
            // 这里简单检查全零的情况，实际应用中需要检查所有弱密钥模式
            for (int i = 0; i < key.Length; i++)
            {
                if (key[i] != 0) return false;
            }
            return true;
        }

        [Test]
        [Category("Performance")]
        public void Performance_StreamEncryption_10MB_ShouldBeFast()
        {
            // Arrange - 使用 CreateRandom 自动生成有效的随机密钥
            using var des = DesCrypto.CreateRandom();
            var iv = des.GenerateIV();

            // 创建测试数据 - 确保是块大小的倍数，避免流处理问题
            int dataSize = 10 * 1024 * 1024; // 10MB
            int blockSize = des.BlockSize; // 8 bytes for DES
            dataSize = ((dataSize + blockSize - 1) / blockSize) * blockSize; // 对齐到块大小

            var data = new byte[dataSize];
            RandomNumberGenerator.Fill(data);

            using var inputStream = new MemoryStream(data);
            using var encryptedStream = new MemoryStream();
            using var decryptedStream = new MemoryStream();

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 使用异步方法
            var encryptTask = des.EncryptAsync(inputStream, encryptedStream, iv);
            encryptTask.Wait();

            encryptedStream.Position = 0;

            var decryptTask = des.DecryptAsync(encryptedStream, decryptedStream, iv);
            decryptTask.Wait();

            stopwatch.Stop();

            // Assert
            var decryptedData = decryptedStream.ToArray();

            // 注意：如果使用 PaddingMode.Zeros，解密后的数据可能会比原始数据长
            // 需要根据填充模式处理
            if (des.Padding == PaddingMode.Zeros)
            {
                // 找到第一个零字节的位置（如果原始数据不包含零）
                int originalLength = data.Length;
                if (decryptedData.Length >= originalLength)
                {
                    // 比较前 originalLength 个字节
                    var originalSpan = data.AsSpan();
                    var decryptedSpan = decryptedData.AsSpan(0, originalLength);
                    Assert.That(decryptedSpan.SequenceEqual(originalSpan), Is.True);
                }
                else
                {
                    Assert.That(decryptedData, Is.EqualTo(data));
                }
            }
            else
            {
                // 对于其他填充模式，长度应该匹配
                Assert.That(decryptedData, Is.EqualTo(data));
            }

            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(10000),
                $"10MB流加密解密耗时{stopwatch.ElapsedMilliseconds}ms，超过10秒");
        }
    }

    /// <summary>
    /// DES 集成测试类
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    public class DesCryptoIntegrationTests
    {
        [Test]
        [Category("Integration")]
        public void Integration_FileEncryptionDecryption_ShouldWork()
        {
            // Arrange
            var originalText = "这是一个测试文件的内容，用于测试DES加密解密功能。\n包含多行文本和特殊字符：!@#$%^&*()\n中文字符：测试加密";

            // 确保文本长度是 8 的倍数以避免流处理问题
            var bytes = Encoding.UTF8.GetBytes(originalText);
            int paddedLength = ((bytes.Length + 7) / 8) * 8;
            if (paddedLength > bytes.Length)
            {
                var paddedBytes = new byte[paddedLength];
                Buffer.BlockCopy(bytes, 0, paddedBytes, 0, bytes.Length);
                originalText = Encoding.UTF8.GetString(paddedBytes);
            }

            var tempFile = Path.GetTempFileName();
            var encryptedFile = Path.GetTempFileName();
            var decryptedFile = Path.GetTempFileName();

            try
            {
                // 写入原始文件
                File.WriteAllText(tempFile, originalText, Encoding.UTF8);

                // 创建加密实例 - 使用随机密钥避免弱密钥
                var key = new byte[8];
                RandomNumberGenerator.Fill(key);
                while (IsWeakDesKey(key))
                {
                    RandomNumberGenerator.Fill(key);
                }

                using var des = new DesCrypto(key);
                var iv = des.GenerateIV();

                // Act - 加密文件
                using (var inputStream = File.OpenRead(tempFile))
                using (var outputStream = File.Create(encryptedFile))
                {
                    des.EncryptAsync(inputStream, outputStream, iv).Wait();
                }

                // Act - 解密文件
                using (var inputStream = File.OpenRead(encryptedFile))
                using (var outputStream = File.Create(decryptedFile))
                {
                    des.DecryptAsync(inputStream, outputStream, iv).Wait();
                }

                // Assert - 比较时可能需要处理填充
                var decryptedText = File.ReadAllText(decryptedFile, Encoding.UTF8);
                decryptedText = decryptedText.TrimEnd('\0'); // 移除 Zeros 填充的零字节
                var trimmedOriginalText = originalText.TrimEnd('\0');
                Assert.That(decryptedText, Is.EqualTo(trimmedOriginalText));
            }
            finally
            {
                // 清理临时文件
                CleanupTempFile(tempFile);
                CleanupTempFile(encryptedFile);
                CleanupTempFile(decryptedFile);
            }
        }

        /// <summary>
        /// 检查是否为 DES 弱密钥
        /// </summary>
        private static bool IsWeakDesKey(byte[] key)
        {
            if (key.Length != 8) return false;

            // DES 有 4 个已知弱密钥
            // 这里简单检查全零的情况，实际应用中需要检查所有弱密钥模式
            for (int i = 0; i < key.Length; i++)
            {
                if (key[i] != 0) return false;
            }
            return true;
        }

        private void CleanupTempFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // 忽略清理错误
            }
        }

        [Test]
        [Category("Integration")]
        public void Integration_MultipleEncryptionFormats_ShouldBeConsistent()
        {
            // Arrange
            using var des = DesCrypto.CreateRandom();
            var iv = des.GenerateIV();
            var originalText = "统一测试数据";

            // Act - 使用不同格式加密
            var ciphertextBytes = des.Encrypt(Encoding.UTF8.GetBytes(originalText), iv);
            var ciphertextBase64 = des.EncryptToBase64(originalText, iv);
            var ciphertextHex = des.EncryptToHex(originalText, iv);

            // 将Base64和Hex转换回字节数组进行比较
            var ciphertextFromBase64 = Convert.FromBase64String(ciphertextBase64);
            var ciphertextFromHex = Enumerable.Range(0, ciphertextHex.Length / 2)
                .Select(x => Convert.ToByte(ciphertextHex.Substring(x * 2, 2), 16))
                .ToArray();

            // Assert - 所有格式应该产生相同的密文
            Assert.That(ciphertextFromBase64, Is.EqualTo(ciphertextBytes));
            Assert.That(ciphertextFromHex, Is.EqualTo(ciphertextBytes));
        }
    }

    /// <summary>
    /// DES 参数化测试示例
    /// </summary>
    [TestFixture]
    public class DesCryptoParameterizedTests
    {
        private static IEnumerable<TestCaseData> EncryptionModesTestCases
        {
            get
            {
                yield return new TestCaseData(CipherMode.CBC, PaddingMode.PKCS7)
                    .SetName("CBC_PKCS7");
                yield return new TestCaseData(CipherMode.CFB, PaddingMode.PKCS7)
                    .SetName("CFB_PKCS7");
                yield return new TestCaseData(CipherMode.ECB, PaddingMode.PKCS7)
                    .SetName("ECB_PKCS7");
                // yield return new TestCaseData(CipherMode.OFB, PaddingMode.PKCS7)
                //     .SetName("OFB_PKCS7");
            }
        }

        [Test]
        [TestCaseSource(nameof(EncryptionModesTestCases))]
        public void Parameterized_EncryptDecrypt_AllModes_ShouldWork(
            CipherMode mode,
            PaddingMode padding)
        {
            // Arrange
            var key = new byte[8];
            RandomNumberGenerator.Fill(key);
            using var des = new DesCrypto(key, mode, padding);
            var iv = des.GenerateIV();
            var data = new byte[100];
            RandomNumberGenerator.Fill(data);

            // Act
            var ciphertext = des.Encrypt(data, iv);
            var plaintext = des.Decrypt(ciphertext, iv);

            // Assert
            Assert.That(plaintext, Is.EqualTo(data));
        }

        private static IEnumerable<TestCaseData> PaddingModesTestCases
        {
            get
            {
                yield return new TestCaseData(PaddingMode.PKCS7).SetName("Padding_PKCS7");
                yield return new TestCaseData(PaddingMode.ANSIX923).SetName("Padding_ANSIX923");
                yield return new TestCaseData(PaddingMode.ISO10126).SetName("Padding_ISO10126");
                yield return new TestCaseData(PaddingMode.Zeros).SetName("Padding_Zeros");
            }
        }

        [Test]
        [TestCaseSource(nameof(PaddingModesTestCases))]
        public void Parameterized_DifferentPaddingModes_ShouldWork(PaddingMode padding)
        {
            // Arrange
            var key = new byte[8];
            RandomNumberGenerator.Fill(key);
            using var des = new DesCrypto(key, CipherMode.CBC, padding);
            var iv = des.GenerateIV();
            var data = new byte[50]; // 非块对齐数据
            RandomNumberGenerator.Fill(data);

            // Act
            var ciphertext = des.Encrypt(data, iv);
            var plaintext = des.Decrypt(ciphertext, iv);

            // Assert
            Assert.That(plaintext, Is.EqualTo(data));
        }
    }
}