#if !BROWSER
using QingYi.Core.Interfaces;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QingYi.Core.Crypto
{
    /// <summary>
    /// Provides Advanced Encryption Standard (AES) cryptographic operations with support for multiple cipher modes.
    /// This class implements the ICrypto interface and offers encryption and decryption functionality using AES algorithm.
    /// It supports standard modes like CBC, ECB, CFB and authenticated encryption with GCM mode.
    /// The class provides both synchronous and asynchronous operations for various data types including byte arrays,
    /// streams, and strings. It implements IDisposable to ensure proper cleanup of cryptographic resources.
    /// </summary>
    public sealed class AesCrypto : ICrypto
    {
#nullable enable
        #region 常量定义

        /// <summary>
        /// Represents the AES block size in bytes (128 bits). AES operates on 16-byte blocks regardless of key size.
        /// </summary>
        private const int AES_BLOCK_SIZE = 16;

        /// <summary>
        /// Represents the AES Initialization Vector (IV) size in bytes (128 bits).
        /// The IV must be unique for each encryption operation when using modes that require it.
        /// </summary>
        private const int AES_IV_SIZE = 16;

        /// <summary>
        /// Represents the default authentication tag size in bytes (128 bits) for GCM mode.
        /// This tag is used for verifying the integrity and authenticity of encrypted data.
        /// </summary>
        private const int DEFAULT_TAG_SIZE = 16;

        /// <summary>
        /// A pre-allocated empty byte array used as a default value for optional byte array parameters.
        /// This helps reduce allocations by avoiding creation of new empty arrays.
        /// </summary>
        private static readonly byte[] EmptyByteArray = [];

        #endregion

        #region 自定义枚举和辅助类

        /// <summary>
        /// Extended enumeration of cipher modes supported by the AesCrypto class.
        /// Includes standard cipher modes as well as GCM (Galois/Counter Mode) for authenticated encryption.
        /// </summary>
        public enum ExtendedCipherMode
        {
            /// <summary>
            /// CBC (Cipher Block Chaining) is a widely-used encryption mode that enhances the security of block ciphers. It operates by combining each plaintext block with the previous ciphertext block before encryption, using an initialization vector (IV) for the first block. This chaining mechanism ensures that identical plaintext blocks produce different ciphertexts, making patterns harder to detect. CBC provides strong confidentiality but requires sequential processing and proper IV management to avoid vulnerabilities. It remains a fundamental choice for secure data transmission and storage in various applications.
            /// </summary>
            CBC,
            /// <summary>
            /// The ECB (Electronic Codebook) mode is a basic and straightforward method for applying a block cipher to encrypt data. In this mode, the input plaintext is divided into fixed-size blocks, each of which is independently encrypted using the same secret key. This approach allows for parallel processing and random access to ciphertext blocks, making it operationally simple. However, ECB has a significant security weakness: identical plaintext blocks always produce identical ciphertext blocks when encrypted with the same key. As a result, patterns in the plaintext—such as repeated sequences or structured data—can remain visible in the ciphertext, potentially leaking information. Due to this limitation, ECB is generally unsuitable for encrypting large or sensitive datasets, and it is not recommended for use in modern cryptographic applications where stronger modes like CBC or GCM are preferred.
            /// </summary>
            ECB,
            /// <summary>
            /// CFB (Cipher Feedback) mode is a symmetric-key block cipher mode of operation that turns a block cipher into a self-synchronizing stream cipher. In CFB mode, the previous ciphertext block is encrypted using the key, and the result is XORed with the current plaintext block to produce ciphertext. This process creates a feedback loop where ciphertext depends on earlier encrypted data, making it suitable for encrypting streaming data (e.g., network communication) where data arrives in real-time. CFB allows for partial block processing and supports error propagation, meaning a transmission error in one ciphertext block affects subsequent decryption until synchronization is regained. However, like other feedback modes, it requires an initialization vector (IV) to ensure security and uniqueness.
            /// </summary>
            CFB,
            /// <summary>
            /// GCM (Galois/Counter Mode) is an efficient and secure authenticated encryption algorithm that combines the Counter (CTR) mode for data confidentiality with a Galois field-based authentication mechanism to ensure data integrity, all in a single pass over the data for high performance in modern applications like TLS and IPSec.
            /// </summary>
            GCM
        }

        #endregion

        #region 字段

        /// <summary>
        /// The underlying AES cryptographic provider used for non-GCM encryption operations.
        /// This field is initialized in the constructor and disposed when this instance is disposed.
        /// </summary>
        private readonly Aes _aes;

        /// <summary>
        /// The AES-GCM cryptographic provider used for authenticated encryption operations.
        /// This field is only initialized when GCM mode is selected and is disposed when this instance is disposed.
        /// </summary>
        private readonly AesGcm? _aesGcm;

        /// <summary>
        /// The cryptographic key used for encryption and decryption operations.
        /// The key length determines the AES variant: 16 bytes for AES-128, 24 bytes for AES-192, or 32 bytes for AES-256.
        /// This array is a copy of the provided key to prevent external modifications.
        /// </summary>
        private byte[] _key;

        /// <summary>
        /// Indicates whether this instance is configured for GCM (Galois/Counter Mode) operation.
        /// GCM provides authenticated encryption, which ensures both confidentiality and integrity of the encrypted data.
        /// </summary>
        private bool _isGcmMode;

        /// <summary>
        /// Tracks whether this instance has been disposed to prevent use after disposal.
        /// Once disposed, all operations on this instance will throw ObjectDisposedException.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Stores the extended cipher mode configured for this instance.
        /// This determines the mode of operation for the AES algorithm.
        /// </summary>
        private ExtendedCipherMode _extendedMode;

        #endregion

        #region 属性

        /// <summary>
        /// Gets a string representation of the cryptographic algorithm and mode being used.
        /// The format is "AES-GCM" for GCM mode or "AES-{KeySize}-{Mode}" for other modes.
        /// </summary>
        /// <value>A string describing the algorithm and mode.</value>
        public string AlgorithmName => _isGcmMode ? "AES-GCM" : $"AES-{KeySize}-{_extendedMode}";

        /// <summary>
        /// Gets the size of the cryptographic key in bits.
        /// Valid sizes are 128, 192, or 256 bits, corresponding to key lengths of 16, 24, or 32 bytes.
        /// </summary>
        /// <value>The key size in bits.</value>
        public int KeySize => _key.Length * 8;

        /// <summary>
        /// Gets the cryptographic key as a read-only memory region.
        /// This provides a safe way to access the key without allowing modifications.
        /// </summary>
        /// <value>A read-only memory region containing the cryptographic key.</value>
        public ReadOnlyMemory<byte> Key => _key;

        /// <summary>
        /// Gets the extended cipher mode being used for encryption and decryption operations.
        /// This property returns the ExtendedCipherMode value that this instance was configured with.
        /// </summary>
        /// <value>The ExtendedCipherMode being used.</value>
        public ExtendedCipherMode ExtendedMode => _extendedMode;

        /// <summary>
        /// Gets a value indicating whether the current mode provides authenticated encryption.
        /// Returns true only for GCM mode, which provides both confidentiality and integrity protection.
        /// </summary>
        /// <value>True if authenticated encryption is enabled (GCM mode); otherwise, false.</value>
        public bool IsAuthenticatedEncryption => _isGcmMode;

        /// <summary>
        /// Gets the standard CipherMode being used for encryption and decryption operations.
        /// This property maps the ExtendedCipherMode to the standard System.Security.Cryptography.CipherMode enumeration.
        /// For GCM mode, this returns CipherMode.CBC as a placeholder since GCM is not part of the standard enumeration.
        /// </summary>
        /// <value>The standard CipherMode being used.</value>
        public CipherMode Mode
        {
            get
            {
                // 将扩展模式映射回标准CipherMode
                return _extendedMode switch
                {
                    ExtendedCipherMode.CBC => CipherMode.CBC,
                    ExtendedCipherMode.ECB => CipherMode.ECB,
                    ExtendedCipherMode.CFB => CipherMode.CFB,
                    ExtendedCipherMode.GCM => CipherMode.CBC, // GCM不是标准CipherMode，返回CBC作为占位
                    _ => CipherMode.CBC
                };
            }
        }

        /// <summary>
        /// Gets the padding mode being used for encryption and decryption operations.
        /// Padding is applied to ensure the plaintext length is a multiple of the block size.
        /// </summary>
        /// <value>The PaddingMode being used.</value>
        public PaddingMode Padding => _aes.Padding;

        /// <summary>
        /// Gets the block size of the AES algorithm in bytes.
        /// AES always uses a block size of 16 bytes (128 bits), regardless of the key size.
        /// </summary>
        /// <value>The block size in bytes.</value>
        public int BlockSize => AES_BLOCK_SIZE;

        /// <summary>
        /// Gets the size of the authentication tag in bytes for authenticated encryption modes.
        /// Returns 16 bytes for GCM mode, or 0 for non-authenticated modes.
        /// The authentication tag is used to verify the integrity and authenticity of the encrypted data.
        /// </summary>
        /// <value>The authentication tag size in bytes.</value>
        public int TagSizeInBytes => _isGcmMode ? DEFAULT_TAG_SIZE : 0;

        #endregion

        #region 构造函数和工厂方法

        /// <summary>
        /// Initializes a new instance of the AesCrypto class with the specified key, cipher mode, and padding mode.
        /// The key is copied internally to prevent external modifications. The key must be 16, 24, or 32 bytes in length.
        /// </summary>
        /// <param name="key">The cryptographic key to use for encryption and decryption. Must be 16, 24, or 32 bytes.</param>
        /// <param name="mode">The cipher mode to use for encryption and decryption. Defaults to CBC.</param>
        /// <param name="padding">The padding mode to use. Defaults to PKCS7. Must be None for GCM mode.</param>
        /// <exception cref="ArgumentNullException">Thrown when the key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the key length is invalid or when incompatible mode and padding are specified.</exception>
        public AesCrypto(byte[] key, ExtendedCipherMode mode = ExtendedCipherMode.CBC, PaddingMode padding = PaddingMode.PKCS7)
        {
            ValidateKey(key);
            ValidateModeAndPadding(mode, padding);

            // 检查模式支持
            if (!IsModeSupported(mode))
            {
                throw new PlatformNotSupportedException(
                    $"Cipher mode {mode} is not supported on this platform. " +
                    $"Supported modes: CBC, ECB, CFB{(IsModeSupported(ExtendedCipherMode.GCM) ? ", GCM" : "")}");
            }

            _key = new byte[key.Length];
            Buffer.BlockCopy(key, 0, _key, 0, key.Length);

            _extendedMode = mode;
            _isGcmMode = mode == ExtendedCipherMode.GCM;

            if (_isGcmMode)
            {
                _aesGcm = new AesGcm(key, DEFAULT_TAG_SIZE);
                _aes = Aes.Create();
                _aes.Mode = CipherMode.CBC;
                _aes.Padding = PaddingMode.None;
            }
            else
            {
                _aes = Aes.Create();
                _aes.Key = key;
                _aes.Mode = mode switch
                {
                    ExtendedCipherMode.CBC => CipherMode.CBC,
                    ExtendedCipherMode.ECB => CipherMode.ECB,
                    ExtendedCipherMode.CFB => CipherMode.CFB,
                    _ => throw new ArgumentException($"Unsupported cipher mode: {mode}")
                };
                _aes.Padding = padding;
                _aes.BlockSize = 128;
            }
        }

        /// <summary>
        /// Creates a new instance of the AesCrypto class from a Base64-encoded key.
        /// This method is useful when the key is stored or transmitted as a Base64 string.
        /// </summary>
        /// <param name="base64Key">A Base64-encoded string representing the cryptographic key.</param>
        /// <param name="mode">The cipher mode to use for encryption and decryption. Defaults to CBC.</param>
        /// <param name="padding">The padding mode to use. Defaults to PKCS7. Must be None for GCM mode.</param>
        /// <returns>A new instance of the AesCrypto class initialized with the decoded key.</returns>
        /// <exception cref="ArgumentNullException">Thrown when base64Key is null.</exception>
        /// <exception cref="FormatException">Thrown when base64Key is not a valid Base64 string.</exception>
        /// <exception cref="ArgumentException">Thrown when the decoded key length is invalid or when incompatible mode and padding are specified.</exception>
        public static AesCrypto CreateFromBase64Key(string base64Key, ExtendedCipherMode mode = ExtendedCipherMode.CBC, PaddingMode padding = PaddingMode.PKCS7)
        {
            byte[] key = Convert.FromBase64String(base64Key);
            return new AesCrypto(key, mode, padding);
        }

        /// <summary>
        /// Creates a new instance of the AesCrypto class from a hexadecimal-encoded key.
        /// This method is useful when the key is stored or transmitted as a hexadecimal string.
        /// </summary>
        /// <param name="hexKey">A hexadecimal-encoded string representing the cryptographic key.</param>
        /// <param name="mode">The cipher mode to use for encryption and decryption. Defaults to CBC.</param>
        /// <param name="padding">The padding mode to use. Defaults to PKCS7. Must be None for GCM mode.</param>
        /// <returns>A new instance of the AesCrypto class initialized with the decoded key.</returns>
        /// <exception cref="ArgumentNullException">Thrown when hexKey is null.</exception>
        /// <exception cref="ArgumentException">Thrown when hexKey is not a valid hexadecimal string or when the decoded key length is invalid, or when incompatible mode and padding are specified.</exception>
        public static AesCrypto CreateFromHexKey(string hexKey, ExtendedCipherMode mode = ExtendedCipherMode.CBC, PaddingMode padding = PaddingMode.PKCS7)
        {
            byte[] key = HexStringToBytes(hexKey);
            return new AesCrypto(key, mode, padding);
        }

        /// <summary>
        /// Creates a new instance of the AesCrypto class with a randomly generated cryptographic key.
        /// This method is recommended for generating new keys for cryptographic operations.
        /// </summary>
        /// <param name="keySize">The size of the key to generate in bits. Must be 128, 192, or 256. Defaults to 256.</param>
        /// <param name="mode">The cipher mode to use for encryption and decryption. Defaults to CBC.</param>
        /// <param name="padding">The padding mode to use. Defaults to PKCS7. Must be None for GCM mode.</param>
        /// <returns>A new instance of the AesCrypto class initialized with a randomly generated key.</returns>
        /// <exception cref="ArgumentException">Thrown when keySize is not 128, 192, or 256, or when incompatible mode and padding are specified.</exception>
        public static AesCrypto CreateRandom(int keySize = 256, ExtendedCipherMode mode = ExtendedCipherMode.CBC, PaddingMode padding = PaddingMode.PKCS7)
        {
            byte[] key = new byte[keySize / 8];
            RandomNumberGenerator.Fill(key);
            return new AesCrypto(key, mode, padding);
        }

        #endregion

        #region 核心加解密方法

        /// <summary>
        /// Encrypts the specified plaintext data using the configured cipher mode and key.
        /// For GCM mode, the method also generates an authentication tag for integrity verification.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <param name="iv">The initialization vector (IV) for the encryption operation. Must be 16 bytes.</param>
        /// <param name="associatedData">Optional additional data to authenticate in GCM mode. Ignored for non-GCM modes.</param>
        /// <returns>The encrypted ciphertext data. For GCM mode, the authentication tag is not included in this result.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when plaintext or iv is null.</exception>
        /// <exception cref="ArgumentException">Thrown when iv is not 16 bytes in length.</exception>
        public byte[] Encrypt(byte[] plaintext, byte[] iv, byte[]? associatedData = null)
        {
            ThrowIfDisposed();
            ValidateIV(iv);

            if (_isGcmMode)
            {
                return EncryptGcm(plaintext, iv, associatedData);
            }
            else
            {
                return EncryptNonGcm(plaintext, iv);
            }
        }

        /// <summary>
        /// Decrypts the specified ciphertext data using the configured cipher mode and key.
        /// For GCM mode, the method also verifies the authentication tag for integrity.
        /// </summary>
        /// <param name="ciphertext">The ciphertext data to decrypt.</param>
        /// <param name="iv">The initialization vector (IV) used for encryption. Must be 16 bytes.</param>
        /// <param name="associatedData">Optional additional data that was authenticated in GCM mode. Ignored for non-GCM modes.</param>
        /// <param name="authenticationTag">The authentication tag for integrity verification in GCM mode. Required for GCM, ignored for non-GCM modes.</param>
        /// <returns>The decrypted plaintext data.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when ciphertext or iv is null.</exception>
        /// <exception cref="ArgumentException">Thrown when iv is not 16 bytes, or when authentication tag is not provided for GCM mode.</exception>
        /// <exception cref="InvalidOperationException">Thrown when authentication tag is provided for non-GCM modes.</exception>
        /// <exception cref="CryptographicException">Thrown when the authentication tag verification fails in GCM mode.</exception>
        public byte[] Decrypt(byte[] ciphertext, byte[] iv, byte[]? associatedData = null, byte[]? authenticationTag = null)
        {
            ThrowIfDisposed();
            ValidateIV(iv);

            if (_isGcmMode)
            {
                if (authenticationTag == null || authenticationTag.Length == 0)
                    throw new ArgumentException("Authentication tag is required for GCM mode");

                return DecryptGcm(ciphertext, iv, authenticationTag, associatedData);
            }
            else
            {
                if (authenticationTag != null && authenticationTag.Length > 0)
                    throw new InvalidOperationException("Authentication tag should not be provided for non-GCM modes");

                return DecryptNonGcm(ciphertext, iv);
            }
        }

        /// <summary>
        /// Encrypts the specified plaintext data using the configured cipher mode and key.
        /// This method uses spans to avoid additional memory allocations.
        /// For GCM mode, the method also generates an authentication tag for integrity verification.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <param name="iv">The initialization vector (IV) for the encryption operation. Must be 16 bytes.</param>
        /// <param name="destination">The span to write the encrypted ciphertext to.</param>
        /// <param name="bytesWritten">When this method returns, contains the number of bytes written to destination.</param>
        /// <param name="associatedData">Optional additional data to authenticate in GCM mode. Ignored for non-GCM modes.</param>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentException">Thrown when iv is not 16 bytes, or when destination is too small.</exception>
        public void Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> iv, Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> associatedData = default)
        {
            ThrowIfDisposed();
            ValidateIV(iv);

            if (_isGcmMode)
            {
                EncryptGcm(plaintext, iv, destination, out bytesWritten, associatedData);
            }
            else
            {
                EncryptNonGcm(plaintext, iv, destination, out bytesWritten);
            }
        }

        /// <summary>
        /// Decrypts the specified ciphertext data using the configured cipher mode and key.
        /// This method uses spans to avoid additional memory allocations.
        /// For GCM mode, the method also verifies the authentication tag for integrity.
        /// </summary>
        /// <param name="ciphertext">The ciphertext data to decrypt.</param>
        /// <param name="iv">The initialization vector (IV) used for encryption. Must be 16 bytes.</param>
        /// <param name="destination">The span to write the decrypted plaintext to.</param>
        /// <param name="bytesWritten">When this method returns, contains the number of bytes written to destination.</param>
        /// <param name="associatedData">Optional additional data that was authenticated in GCM mode. Ignored for non-GCM modes.</param>
        /// <param name="authenticationTag">The authentication tag for integrity verification in GCM mode. Required for GCM, ignored for non-GCM modes.</param>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentException">Thrown when iv is not 16 bytes, when destination is too small, or when authentication tag is not provided for GCM mode.</exception>
        /// <exception cref="InvalidOperationException">Thrown when authentication tag is provided for non-GCM modes.</exception>
        /// <exception cref="CryptographicException">Thrown when the authentication tag verification fails in GCM mode.</exception>
        public void Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> iv, Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> associatedData = default, ReadOnlySpan<byte> authenticationTag = default)
        {
            ThrowIfDisposed();
            ValidateIV(iv);

            if (_isGcmMode)
            {
                if (authenticationTag.IsEmpty)
                    throw new ArgumentException("Authentication tag is required for GCM mode");

                DecryptGcm(ciphertext, iv, destination, out bytesWritten, authenticationTag, associatedData);
            }
            else
            {
                if (!authenticationTag.IsEmpty)
                    throw new InvalidOperationException("Authentication tag should not be provided for non-GCM modes");

                DecryptNonGcm(ciphertext, iv, destination, out bytesWritten);
            }
        }

        #endregion

        #region GCM模式加解密实现

        /// <summary>
        /// Encrypts the specified plaintext data using AES-GCM mode.
        /// This method generates an authentication tag for integrity verification.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <param name="iv">The initialization vector (IV) for the encryption operation. Must be 12 bytes for GCM.</param>
        /// <param name="associatedData">Optional additional data to authenticate. This data is not encrypted but is included in the authentication tag calculation.</param>
        /// <returns>The encrypted ciphertext data. The authentication tag is not included in this result.</returns>
        /// <exception cref="CryptographicException">Thrown when the encryption operation fails.</exception>
        private byte[] EncryptGcm(byte[] plaintext, byte[] iv, byte[]? associatedData)
        {
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[DEFAULT_TAG_SIZE];

            if (associatedData == null || associatedData.Length == 0)
            {
                _aesGcm!.Encrypt(iv, plaintext, ciphertext, tag);
            }
            else
            {
                _aesGcm!.Encrypt(iv, plaintext, ciphertext, tag, associatedData);
            }

            return ciphertext;
        }

        /// <summary>
        /// Encrypts the specified plaintext data using AES-GCM mode with spans to avoid additional memory allocations.
        /// This method generates an authentication tag for integrity verification.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <param name="iv">The initialization vector (IV) for the encryption operation. Must be 12 bytes for GCM.</param>
        /// <param name="destination">The span to write the encrypted ciphertext to.</param>
        /// <param name="bytesWritten">When this method returns, contains the number of bytes written to destination.</param>
        /// <param name="associatedData">Optional additional data to authenticate. This data is not encrypted but is included in the authentication tag calculation.</param>
        /// <exception cref="CryptographicException">Thrown when the encryption operation fails.</exception>
        private void EncryptGcm(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> iv, Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> associatedData)
        {
            bytesWritten = plaintext.Length;
            Span<byte> tag = stackalloc byte[DEFAULT_TAG_SIZE];

            if (associatedData.IsEmpty)
            {
                _aesGcm!.Encrypt(iv, plaintext, destination, tag);
            }
            else
            {
                _aesGcm!.Encrypt(iv, plaintext, destination, tag, associatedData);
            }
        }

        /// <summary>
        /// Decrypts the specified ciphertext data using AES-GCM mode.
        /// This method verifies the authentication tag for integrity before returning the plaintext.
        /// </summary>
        /// <param name="ciphertext">The ciphertext data to decrypt.</param>
        /// <param name="iv">The initialization vector (IV) used for encryption. Must be 12 bytes for GCM.</param>
        /// <param name="tag">The authentication tag for integrity verification. Must be 16 bytes.</param>
        /// <param name="associatedData">Optional additional data that was authenticated during encryption. This data is not encrypted but is included in the authentication tag calculation.</param>
        /// <returns>The decrypted plaintext data.</returns>
        /// <exception cref="CryptographicException">Thrown when the authentication tag verification fails or when the decryption operation fails.</exception>
        private byte[] DecryptGcm(byte[] ciphertext, byte[] iv, byte[] tag, byte[]? associatedData)
        {
            byte[] plaintext = new byte[ciphertext.Length];

            if (associatedData == null || associatedData.Length == 0)
            {
                _aesGcm!.Decrypt(iv, ciphertext, tag, plaintext);
            }
            else
            {
                _aesGcm!.Decrypt(iv, ciphertext, tag, plaintext, associatedData);
            }

            return plaintext;
        }

        /// <summary>
        /// Decrypts the specified ciphertext data using AES-GCM mode with spans to avoid additional memory allocations.
        /// This method verifies the authentication tag for integrity before returning the plaintext.
        /// </summary>
        /// <param name="ciphertext">The ciphertext data to decrypt.</param>
        /// <param name="iv">The initialization vector (IV) used for encryption. Must be 12 bytes for GCM.</param>
        /// <param name="destination">The span to write the decrypted plaintext to.</param>
        /// <param name="bytesWritten">When this method returns, contains the number of bytes written to destination.</param>
        /// <param name="tag">The authentication tag for integrity verification. Must be 16 bytes.</param>
        /// <param name="associatedData">Optional additional data that was authenticated during encryption. This data is not encrypted but is included in the authentication tag calculation.</param>
        /// <exception cref="CryptographicException">Thrown when the authentication tag verification fails or when the decryption operation fails.</exception>
        private void DecryptGcm(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> iv, Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> associatedData)
        {
            bytesWritten = ciphertext.Length;

            if (associatedData.IsEmpty)
            {
                _aesGcm!.Decrypt(iv, ciphertext, tag, destination);
            }
            else
            {
                _aesGcm!.Decrypt(iv, ciphertext, tag, destination, associatedData);
            }
        }

        #endregion

        #region 非GCM模式加解密实现

        /// <summary>
        /// Encrypts the specified plaintext data using a non-GCM cipher mode (CBC, ECB, CFB).
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <param name="iv">The initialization vector (IV) for the encryption operation. Must be 16 bytes.</param>
        /// <returns>The encrypted ciphertext data.</returns>
        /// <exception cref="CryptographicException">Thrown when the encryption operation fails.</exception>
        private byte[] EncryptNonGcm(byte[] plaintext, byte[] iv)
        {
            using var encryptor = _aes.CreateEncryptor(_key, iv);
            return encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        }

        /// <summary>
        /// Encrypts the specified plaintext data using a non-GCM cipher mode (CBC, ECB, CFB) with spans to avoid additional memory allocations.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <param name="iv">The initialization vector (IV) for the encryption operation. Must be 16 bytes.</param>
        /// <param name="destination">The span to write the encrypted ciphertext to.</param>
        /// <param name="bytesWritten">When this method returns, contains the number of bytes written to destination.</param>
        /// <exception cref="CryptographicException">Thrown when the encryption operation fails.</exception>
        private void EncryptNonGcm(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> iv, Span<byte> destination, out int bytesWritten)
        {
            // 将 span 转换为数组以兼容 ICryptoTransform API
            byte[] plaintextArray = plaintext.ToArray();
            byte[] ivArray = iv.ToArray();

            using var encryptor = _aes.CreateEncryptor(_key, ivArray);

            // 处理完整块
            int totalTransformed = 0;
            int blockSize = encryptor.InputBlockSize;
            int bytesToProcess = plaintextArray.Length;
            int inputIndex = 0;

            // 如果有完整块，使用 TransformBlock
            if (bytesToProcess >= blockSize)
            {
                // 计算完整块的数量（除了最后一个块）
                int fullBlocks = (bytesToProcess / blockSize) - 1;

                for (int i = 0; i < fullBlocks; i++)
                {
                    int transformed = encryptor.TransformBlock(
                        plaintextArray, inputIndex, blockSize,
                        destination.ToArray(), totalTransformed);

                    inputIndex += blockSize;
                    totalTransformed += transformed;
                }
            }

            // 处理最后一个块（可能是不完整块，需要 TransformFinalBlock）
            byte[] finalBlock = encryptor.TransformFinalBlock(
                plaintextArray, inputIndex, plaintextArray.Length - inputIndex);

            // 将最后一个块复制到目标位置
            finalBlock.AsSpan().CopyTo(destination[totalTransformed..]);
            totalTransformed += finalBlock.Length;

            bytesWritten = totalTransformed;
        }

        /// <summary>
        /// Decrypts the specified ciphertext data using a non-GCM cipher mode (CBC, ECB, CFB).
        /// </summary>
        /// <param name="ciphertext">The ciphertext data to decrypt.</param>
        /// <param name="iv">The initialization vector (IV) used for encryption. Must be 16 bytes.</param>
        /// <returns>The decrypted plaintext data.</returns>
        /// <exception cref="CryptographicException">Thrown when the decryption operation fails.</exception>
        private byte[] DecryptNonGcm(byte[] ciphertext, byte[] iv)
        {
            using var decryptor = _aes.CreateDecryptor(_key, iv);
            return decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        }

        /// <summary>
        /// Decrypts the specified ciphertext data using a non-GCM cipher mode (CBC, ECB, CFB) with spans to avoid additional memory allocations.
        /// </summary>
        /// <param name="ciphertext">The ciphertext data to decrypt.</param>
        /// <param name="iv">The initialization vector (IV) used for encryption. Must be 16 bytes.</param>
        /// <param name="destination">The span to write the decrypted plaintext to.</param>
        /// <param name="bytesWritten">When this method returns, contains the number of bytes written to destination.</param>
        /// <exception cref="CryptographicException">Thrown when the decryption operation fails.</exception>
        private void DecryptNonGcm(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> iv, Span<byte> destination, out int bytesWritten)
        {
            // 将 span 转换为数组以兼容 ICryptoTransform API
            byte[] ciphertextArray = ciphertext.ToArray();
            byte[] ivArray = iv.ToArray();

            using var decryptor = _aes.CreateDecryptor(_key, ivArray);

            // 处理完整块
            int totalTransformed = 0;
            int blockSize = decryptor.InputBlockSize;
            int bytesToProcess = ciphertextArray.Length;
            int inputIndex = 0;

            // 如果有完整块，使用 TransformBlock
            if (bytesToProcess >= blockSize)
            {
                // 计算完整块的数量（除了最后一个块）
                int fullBlocks = (bytesToProcess / blockSize) - 1;

                for (int i = 0; i < fullBlocks; i++)
                {
                    int transformed = decryptor.TransformBlock(
                        ciphertextArray, inputIndex, blockSize,
                        destination.ToArray(), totalTransformed);

                    inputIndex += blockSize;
                    totalTransformed += transformed;
                }
            }

            // 处理最后一个块（可能是不完整块，需要 TransformFinalBlock）
            byte[] finalBlock = decryptor.TransformFinalBlock(
                ciphertextArray, inputIndex, ciphertextArray.Length - inputIndex);

            // 将最后一个块复制到目标位置
            finalBlock.AsSpan().CopyTo(destination[totalTransformed..]);
            totalTransformed += finalBlock.Length;

            bytesWritten = totalTransformed;
        }

        /// <summary>
        /// Attempts to decrypt the specified ciphertext data using spans.
        /// Returns true if the operation succeeded; otherwise, false.
        /// </summary>
        public bool TryDecrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> iv, Span<byte> destination, out int bytesWritten)
        {
            try
            {
                Decrypt(ciphertext, iv, destination, out bytesWritten);
                return true;
            }
            catch
            {
                bytesWritten = 0;
                return false;
            }
        }

        #endregion

        #region 字符串便捷方法

        /// <summary>
        /// Encrypts a UTF-8 encoded string and returns the result as a Base64-encoded string.
        /// This method is a convenient way to encrypt text data for storage or transmission.
        /// </summary>
        /// <param name="plaintext">The plaintext string to encrypt.</param>
        /// <param name="iv">The initialization vector (IV) for the encryption operation. Must be 16 bytes.</param>
        /// <param name="associatedData">Optional additional data to authenticate in GCM mode. Ignored for non-GCM modes.</param>
        /// <returns>A Base64-encoded string representing the encrypted ciphertext.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when plaintext or iv is null.</exception>
        /// <exception cref="ArgumentException">Thrown when iv is not 16 bytes in length.</exception>
        public string EncryptToBase64(string plaintext, byte[] iv, byte[]? associatedData = null)
        {
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] ciphertext = Encrypt(plaintextBytes, iv, associatedData);
            return Convert.ToBase64String(ciphertext);
        }

        /// <summary>
        /// Encrypts a UTF-8 encoded string and returns the result as a hexadecimal-encoded string.
        /// This method is a convenient way to encrypt text data for storage or transmission.
        /// </summary>
        /// <param name="plaintext">The plaintext string to encrypt.</param>
        /// <param name="iv">The initialization vector (IV) for the encryption operation. Must be 16 bytes.</param>
        /// <param name="associatedData">Optional additional data to authenticate in GCM mode. Ignored for non-GCM modes.</param>
        /// <returns>A hexadecimal-encoded string representing the encrypted ciphertext.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when plaintext or iv is null.</exception>
        /// <exception cref="ArgumentException">Thrown when iv is not 16 bytes in length.</exception>
        public string EncryptToHex(string plaintext, byte[] iv, byte[]? associatedData = null)
        {
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] ciphertext = Encrypt(plaintextBytes, iv, associatedData);
            return BytesToHexString(ciphertext);
        }

        /// <summary>
        /// Decrypts a Base64-encoded ciphertext string and returns the result as a UTF-8 encoded string.
        /// This method is a convenient way to decrypt text data that was stored or transmitted as Base64.
        /// </summary>
        /// <param name="base64Ciphertext">The Base64-encoded ciphertext string to decrypt.</param>
        /// <param name="iv">The initialization vector (IV) used for encryption. Must be 16 bytes.</param>
        /// <param name="associatedData">Optional additional data that was authenticated in GCM mode. Ignored for non-GCM modes.</param>
        /// <param name="tagBase64">The authentication tag for integrity verification in GCM mode. Required for GCM, ignored for non-GCM modes.</param>
        /// <returns>The decrypted plaintext as a UTF-8 encoded string.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when base64Ciphertext or iv is null.</exception>
        /// <exception cref="ArgumentException">Thrown when iv is not 16 bytes, or when authentication tag is not provided for GCM mode.</exception>
        /// <exception cref="FormatException">Thrown when base64Ciphertext or tagBase64 is not a valid Base64 string.</exception>
        /// <exception cref="CryptographicException">Thrown when the authentication tag verification fails in GCM mode.</exception>
        public string DecryptFromBase64(string base64Ciphertext, byte[] iv, byte[]? associatedData = null, byte[]? tagBase64 = null)
        {
            byte[] ciphertext = Convert.FromBase64String(base64Ciphertext);
            byte[]? tag = tagBase64 != null ? Convert.FromBase64String(Convert.ToBase64String(tagBase64)) : null;
            byte[] plaintext = Decrypt(ciphertext, iv, associatedData, tag);
            return Encoding.UTF8.GetString(plaintext);
        }

        /// <summary>
        /// Decrypts a hexadecimal-encoded ciphertext string and returns the result as a UTF-8 encoded string.
        /// This method is a convenient way to decrypt text data that was stored or transmitted as hexadecimal.
        /// </summary>
        /// <param name="hexCiphertext">The hexadecimal-encoded ciphertext string to decrypt.</param>
        /// <param name="iv">The initialization vector (IV) used for encryption. Must be 16 bytes.</param>
        /// <param name="associatedData">Optional additional data that was authenticated in GCM mode. Ignored for non-GCM modes.</param>
        /// <param name="tagHex">The authentication tag for integrity verification in GCM mode, as a hexadecimal string. Required for GCM, ignored for non-GCM modes.</param>
        /// <returns>The decrypted plaintext as a UTF-8 encoded string.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when hexCiphertext or iv is null.</exception>
        /// <exception cref="ArgumentException">Thrown when iv is not 16 bytes, when hexCiphertext or tagHex is not a valid hexadecimal string, or when authentication tag is not provided for GCM mode.</exception>
        /// <exception cref="CryptographicException">Thrown when the authentication tag verification fails in GCM mode.</exception>
        public string DecryptFromHex(string hexCiphertext, byte[] iv, byte[]? associatedData = null, string? tagHex = null)
        {
            byte[] ciphertext = HexStringToBytes(hexCiphertext);
            byte[]? tag = tagHex != null ? HexStringToBytes(tagHex) : null;
            byte[] plaintext = Decrypt(ciphertext, iv, associatedData, tag);
            return Encoding.UTF8.GetString(plaintext);
        }

        #endregion

        #region 流处理方法

        /// <summary>
        /// Creates a symmetric encryptor object with the specified key and initialization vector (IV).
        /// This method is not supported for GCM mode; use streaming methods instead.
        /// </summary>
        /// <param name="iv">The initialization vector (IV) for the symmetric algorithm. Must be 16 bytes.</param>
        /// <param name="associatedData">Optional additional data to authenticate in GCM mode. Ignored for non-GCM modes.</param>
        /// <returns>A symmetric encryptor object.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this method is called in GCM mode.</exception>
        public ICryptoTransform CreateEncryptor(byte[] iv, byte[]? associatedData = null)
        {
            ThrowIfDisposed();

            if (_isGcmMode)
                throw new NotSupportedException("ICryptoTransform is not supported for GCM mode. Use streaming methods instead.");

            return _aes.CreateEncryptor(_key, iv);
        }

        /// <summary>
        /// Creates a symmetric decryptor object with the specified key and initialization vector (IV).
        /// This method is not supported for GCM mode; use streaming methods instead.
        /// </summary>
        /// <param name="iv">The initialization vector (IV) for the symmetric algorithm. Must be 16 bytes.</param>
        /// <param name="associatedData">Optional additional data that was authenticated in GCM mode. Ignored for non-GCM modes.</param>
        /// <param name="authenticationTag">The authentication tag for integrity verification in GCM mode. Required for GCM, ignored for non-GCM modes.</param>
        /// <returns>A symmetric decryptor object.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this method is called in GCM mode.</exception>
        /// <exception cref="ArgumentException">Thrown when authentication tag is provided for non-GCM modes.</exception>
        public ICryptoTransform CreateDecryptor(byte[] iv, byte[]? associatedData = null, byte[]? authenticationTag = null)
        {
            ThrowIfDisposed();

            if (_isGcmMode)
                throw new NotSupportedException("ICryptoTransform is not supported for GCM mode. Use streaming methods instead.");

            if (authenticationTag != null && authenticationTag.Length > 0)
                throw new ArgumentException("Authentication tag is not supported for non-GCM modes");

            return _aes.CreateDecryptor(_key, iv);
        }

        /// <summary>
        /// Asynchronously encrypts data from the input stream and writes the encrypted data to the output stream.
        /// For GCM mode, the entire input stream is read into memory before encryption.
        /// </summary>
        /// <param name="plaintextStream">The stream containing the plaintext data to encrypt.</param>
        /// <param name="ciphertextStream">The stream to write the encrypted ciphertext to.</param>
        /// <param name="iv">The initialization vector (IV) for the encryption operation. Must be 16 bytes.</param>
        /// <param name="associatedData">Optional additional data to authenticate in GCM mode. Ignored for non-GCM modes.</param>
        /// <param name="progress">Optional progress reporter for tracking encryption progress.</param>
        /// <param name="ct">A cancellation token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when plaintextStream, ciphertextStream, or iv is null.</exception>
        /// <exception cref="ArgumentException">Thrown when iv is not 16 bytes in length.</exception>
        public async Task EncryptAsync(Stream plaintextStream, Stream ciphertextStream, byte[] iv,
            byte[]? associatedData = null, IProgress<long>? progress = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ValidateIV(iv);

            // 为不同模式创建不同的处理逻辑
            if (_isGcmMode)
            {
                await ProcessGcmEncryptionAsync(plaintextStream, ciphertextStream, iv, associatedData, progress, ct);
            }
            else
            {
                await ProcessNonGcmEncryptionAsync(plaintextStream, ciphertextStream, iv, progress, ct);
            }
        }

        /// <summary>
        /// Asynchronously decrypts data from the input stream and writes the decrypted data to the output stream.
        /// For GCM mode, the entire input stream is read into memory before decryption.
        /// </summary>
        /// <param name="ciphertextStream">The stream containing the ciphertext data to decrypt.</param>
        /// <param name="plaintextStream">The stream to write the decrypted plaintext to.</param>
        /// <param name="iv">The initialization vector (IV) used for encryption. Must be 16 bytes.</param>
        /// <param name="associatedData">Optional additional data that was authenticated in GCM mode. Ignored for non-GCM modes.</param>
        /// <param name="authenticationTag">The authentication tag for integrity verification in GCM mode. Required for GCM, ignored for non-GCM modes.</param>
        /// <param name="progress">Optional progress reporter for tracking decryption progress.</param>
        /// <param name="ct">A cancellation token to cancel the operation.</param>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when ciphertextStream, plaintextStream, or iv is null.</exception>
        /// <exception cref="ArgumentException">Thrown when iv is not 16 bytes, or when authentication tag is not provided for GCM mode.</exception>
        /// <exception cref="CryptographicException">Thrown when the authentication tag verification fails in GCM mode.</exception>
        public async Task DecryptAsync(Stream ciphertextStream, Stream plaintextStream, byte[] iv,
            byte[]? associatedData = null, byte[]? authenticationTag = null,
            IProgress<long>? progress = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            ValidateIV(iv);

            if (_isGcmMode)
            {
                if (authenticationTag == null || authenticationTag.Length == 0)
                    throw new ArgumentException("Authentication tag is required for GCM mode");

                await ProcessGcmDecryptionAsync(ciphertextStream, plaintextStream, iv, authenticationTag, associatedData, progress, ct);
            }
            else
            {
                await ProcessNonGcmDecryptionAsync(ciphertextStream, plaintextStream, iv, progress, ct);
            }
        }

        /// <summary>
        /// Asynchronously encrypts data from the input stream and writes the encrypted data to the output stream using non-GCM cipher modes.
        /// This method processes data in chunks to support large files without loading the entire file into memory.
        /// </summary>
        /// <param name="input">The stream containing the plaintext data to encrypt.</param>
        /// <param name="output">The stream to write the encrypted ciphertext to.</param>
        /// <param name="iv">The initialization vector (IV) for the encryption operation. Must be 16 bytes.</param>
        /// <param name="progress">Optional progress reporter for tracking encryption progress.</param>
        /// <param name="ct">A cancellation token to cancel the operation.</param>
        /// <exception cref="CryptographicException">Thrown when the encryption operation fails.</exception>
        private async Task ProcessNonGcmEncryptionAsync(Stream input, Stream output, byte[] iv,
    IProgress<long>? progress, CancellationToken ct)
        {
#pragma warning disable CS0219 // 变量已被赋值，但从未使用过它的值
            const int bufferSize = 81920; // 80KB缓冲区
#pragma warning restore CS0219 // 变量已被赋值，但从未使用过它的值

            // 使用一次性读取并处理，确保数据完整
            using var ms = new MemoryStream();
            await input.CopyToAsync(ms, ct);
            byte[] allData = ms.ToArray();

            // 使用加密器一次性处理所有数据
            using var encryptor = _aes.CreateEncryptor(_key, iv);
            byte[] encryptedData = encryptor.TransformFinalBlock(allData, 0, allData.Length);

            await output.WriteAsync(encryptedData, 0, encryptedData.Length, ct);
            progress?.Report(allData.Length);
        }

        /// <summary>
        /// Asynchronously encrypts data from the input stream and writes the encrypted data to the output stream using GCM mode.
        /// Note that GCM mode does not support true streaming encryption, so the entire input stream is read into memory before encryption.
        /// This method is not recommended for very large files due to memory constraints.
        /// </summary>
        /// <param name="input">The stream containing the plaintext data to encrypt.</param>
        /// <param name="output">The stream to write the encrypted ciphertext to.</param>
        /// <param name="iv">The initialization vector (IV) for the encryption operation. Must be 12 bytes for GCM.</param>
        /// <param name="associatedData">Optional additional data to authenticate. This data is not encrypted but is included in the authentication tag calculation.</param>
        /// <param name="progress">Optional progress reporter for tracking encryption progress.</param>
        /// <param name="ct">A cancellation token to cancel the operation.</param>
        /// <exception cref="CryptographicException">Thrown when the encryption operation fails.</exception>
        private async Task ProcessGcmEncryptionAsync(Stream input, Stream output, byte[] iv, byte[]? associatedData,
            IProgress<long>? progress, CancellationToken ct)
        {
            // GCM模式不支持流式加密，需要一次性处理
            using var ms = new MemoryStream();
            await input.CopyToAsync(ms, ct);
            byte[] plaintext = ms.ToArray();

            byte[] ciphertext = EncryptGcm(plaintext, iv, associatedData);

            await output.WriteAsync(ciphertext, 0, ciphertext.Length, ct);
            progress?.Report(plaintext.Length);
        }

        /// <summary>
        /// Asynchronously decrypts data from the input stream and writes the decrypted data to the output stream using non-GCM cipher modes.
        /// This method processes data in chunks to support large files without loading the entire file into memory.
        /// </summary>
        /// <param name="input">The stream containing the ciphertext data to decrypt.</param>
        /// <param name="output">The stream to write the decrypted plaintext to.</param>
        /// <param name="iv">The initialization vector (IV) used for encryption. Must be 16 bytes.</param>
        /// <param name="progress">Optional progress reporter for tracking decryption progress.</param>
        /// <param name="ct">A cancellation token to cancel the operation.</param>
        /// <exception cref="CryptographicException">Thrown when the decryption operation fails.</exception>
        private async Task ProcessNonGcmDecryptionAsync(Stream input, Stream output, byte[] iv, IProgress<long>? progress, CancellationToken ct)
        {
            // 一次性读取所有数据
            using var ms = new MemoryStream();
            await input.CopyToAsync(ms, ct);
            byte[] allData = ms.ToArray();

            // 使用解密器一次性处理所有数据
            using var decryptor = _aes.CreateDecryptor(_key, iv);
            byte[] decryptedData = decryptor.TransformFinalBlock(allData, 0, allData.Length);

            await output.WriteAsync(decryptedData, 0, decryptedData.Length, ct);
            progress?.Report(allData.Length);
        }

        /// <summary>
        /// Asynchronously decrypts data from the input stream and writes the decrypted data to the output stream using GCM mode.
        /// Note that GCM mode does not support true streaming decryption, so the entire input stream is read into memory before decryption.
        /// This method also verifies the authentication tag for integrity before returning the plaintext.
        /// This method is not recommended for very large files due to memory constraints.
        /// </summary>
        /// <param name="input">The stream containing the ciphertext data to decrypt.</param>
        /// <param name="output">The stream to write the decrypted plaintext to.</param>
        /// <param name="iv">The initialization vector (IV) used for encryption. Must be 12 bytes for GCM.</param>
        /// <param name="tag">The authentication tag for integrity verification. Must be 16 bytes.</param>
        /// <param name="associatedData">Optional additional data that was authenticated during encryption. This data is not encrypted but is included in the authentication tag calculation.</param>
        /// <param name="progress">Optional progress reporter for tracking decryption progress.</param>
        /// <param name="ct">A cancellation token to cancel the operation.</param>
        /// <exception cref="CryptographicException">Thrown when the authentication tag verification fails or when the decryption operation fails.</exception>
        private async Task ProcessGcmDecryptionAsync(Stream input, Stream output, byte[] iv, byte[] tag, byte[]? associatedData,
            IProgress<long>? progress, CancellationToken ct)
        {
            // GCM模式不支持流式解密，需要一次性处理
            using var ms = new MemoryStream();
            await input.CopyToAsync(ms, ct);
            byte[] ciphertext = ms.ToArray();

            byte[] plaintext = DecryptGcm(ciphertext, iv, tag, associatedData);

            await output.WriteAsync(plaintext, 0, plaintext.Length, ct);
            progress?.Report(ciphertext.Length);
        }

        #endregion

        #region 随机数生成

        /// <summary>
        /// Generates a cryptographically secure random initialization vector (IV) for use with the configured cipher mode.
        /// The IV is 16 bytes (128 bits) in length for all modes except GCM.
        /// </summary>
        /// <returns>A cryptographically secure random IV of 16 bytes.</returns>
        public byte[] GenerateIV()
        {
            return GenerateRandomBytes(AES_IV_SIZE);
        }

        /// <summary>
        /// Generates a cryptographically secure random nonce for use with the configured cipher mode.
        /// For GCM mode, this generates a 12-byte nonce, which is the recommended size for GCM.
        /// For other modes, this generates a 16-byte nonce, equivalent to an IV.
        /// </summary>
        /// <returns>A cryptographically secure random nonce (12 bytes for GCM mode, 16 bytes for other modes).</returns>
        public byte[] GenerateNonce()
        {
            // 对于GCM模式，推荐使用12字节的nonce
            return GenerateRandomBytes(_isGcmMode ? 12 : AES_IV_SIZE);
        }

        /// <summary>
        /// Generates a cryptographically secure random sequence of bytes of the specified length.
        /// This method uses a cryptographically secure random number generator to ensure the randomness
        /// is suitable for cryptographic operations.
        /// </summary>
        /// <param name="byteCount">The number of random bytes to generate.</param>
        /// <returns>A cryptographically secure random byte array of the specified length.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when byteCount is negative.</exception>
        public byte[] GenerateRandomBytes(int byteCount)
        {
            byte[] bytes = new byte[byteCount];
            RandomNumberGenerator.Fill(bytes);
            return bytes;
        }

        #endregion

        #region 业务友好方法

        /// <summary>
        /// Encrypts the specified plaintext data and prepends a randomly generated initialization vector (IV) to the result.
        /// For GCM mode, the authentication tag is also appended to the result.
        /// This method is convenient for scenarios where the IV needs to be stored or transmitted with the ciphertext.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <param name="associatedData">Optional additional data to authenticate in GCM mode. Ignored for non-GCM modes.</param>
        /// <returns>A byte array containing the IV, ciphertext, and (for GCM mode) the authentication tag.
        /// For non-GCM modes: IV (16 bytes) + ciphertext.
        /// For GCM mode: IV (16 bytes) + ciphertext + authentication tag (16 bytes).</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when plaintext is null.</exception>
        /// <exception cref="CryptographicException">Thrown when the encryption operation fails.</exception>
        public byte[] EncryptWithPrefixIV(byte[] plaintext, byte[]? associatedData = null)
        {
            byte[] iv = GenerateIV();
            byte[] ciphertext;
            byte[] tag = EmptyByteArray;

            if (_isGcmMode)
            {
                ciphertext = new byte[plaintext.Length];
                tag = new byte[DEFAULT_TAG_SIZE];

                if (associatedData == null || associatedData.Length == 0)
                {
                    _aesGcm!.Encrypt(iv, plaintext, ciphertext, tag);
                }
                else
                {
                    _aesGcm!.Encrypt(iv, plaintext, ciphertext, tag, associatedData);
                }

                // 返回格式: iv + ciphertext + tag
                byte[] result = new byte[iv.Length + ciphertext.Length + tag.Length];
                Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                Buffer.BlockCopy(ciphertext, 0, result, iv.Length, ciphertext.Length);
                Buffer.BlockCopy(tag, 0, result, iv.Length + ciphertext.Length, tag.Length);
                return result;
            }
            else
            {
                ciphertext = Encrypt(plaintext, iv);

                // 返回格式: iv + ciphertext
                byte[] result = new byte[iv.Length + ciphertext.Length];
                Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                Buffer.BlockCopy(ciphertext, 0, result, iv.Length, ciphertext.Length);
                return result;
            }
        }

        /// <summary>
        /// Decrypts the specified combined data that contains an IV, ciphertext, and (for GCM mode) an authentication tag.
        /// This method is the counterpart to EncryptWithPrefixIV and is convenient for scenarios where the IV is stored or transmitted with the ciphertext.
        /// </summary>
        /// <param name="combinedData">The combined data containing IV, ciphertext, and (for GCM mode) authentication tag.
        /// For non-GCM modes: IV (16 bytes) + ciphertext.
        /// For GCM mode: IV (16 bytes) + ciphertext + authentication tag (16 bytes).</param>
        /// <param name="associatedData">Optional additional data that was authenticated in GCM mode. Ignored for non-GCM modes.</param>
        /// <returns>The decrypted plaintext data.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when combinedData is null.</exception>
        /// <exception cref="ArgumentException">Thrown when combinedData has insufficient length for the expected format.</exception>
        /// <exception cref="CryptographicException">Thrown when the authentication tag verification fails in GCM mode or when the decryption operation fails.</exception>
        public byte[] DecryptWithPrefixIV(byte[] combinedData, byte[]? associatedData = null)
        {
            if (_isGcmMode)
            {
                // 格式: iv(16) + ciphertext + tag(16)
                if (combinedData.Length < AES_IV_SIZE + DEFAULT_TAG_SIZE)
                    throw new ArgumentException("Invalid combined data length for GCM mode");

                byte[] iv = new byte[AES_IV_SIZE];
                byte[] tag = new byte[DEFAULT_TAG_SIZE];
                byte[] ciphertext = new byte[combinedData.Length - AES_IV_SIZE - DEFAULT_TAG_SIZE];

                Buffer.BlockCopy(combinedData, 0, iv, 0, AES_IV_SIZE);
                Buffer.BlockCopy(combinedData, AES_IV_SIZE, ciphertext, 0, ciphertext.Length);
                Buffer.BlockCopy(combinedData, AES_IV_SIZE + ciphertext.Length, tag, 0, DEFAULT_TAG_SIZE);

                return DecryptGcm(ciphertext, iv, tag, associatedData);
            }
            else
            {
                // 格式: iv(16) + ciphertext
                if (combinedData.Length < AES_IV_SIZE)
                    throw new ArgumentException("Invalid combined data length");

                byte[] iv = new byte[AES_IV_SIZE];
                byte[] ciphertext = new byte[combinedData.Length - AES_IV_SIZE];

                Buffer.BlockCopy(combinedData, 0, iv, 0, AES_IV_SIZE);
                Buffer.BlockCopy(combinedData, AES_IV_SIZE, ciphertext, 0, ciphertext.Length);

                return Decrypt(ciphertext, iv);
            }
        }

        /// <summary>
        /// Encrypts the specified plaintext data with a prepended IV and returns the result as a Base64-encoded string.
        /// This method is convenient for scenarios where the encrypted data needs to be stored or transmitted as a Base64 string.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <param name="associatedData">Optional additional data to authenticate in GCM mode. Ignored for non-GCM modes.</param>
        /// <returns>A Base64-encoded string containing the IV, ciphertext, and (for GCM mode) the authentication tag.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when plaintext is null.</exception>
        /// <exception cref="CryptographicException">Thrown when the encryption operation fails.</exception>
        public string EncryptWithPrefixIVToBase64(byte[] plaintext, byte[]? associatedData = null)
        {
            byte[] combined = EncryptWithPrefixIV(plaintext, associatedData);
            return Convert.ToBase64String(combined);
        }

        /// <summary>
        /// Decrypts a Base64-encoded string containing combined data with a prepended IV and returns the plaintext.
        /// This method is the counterpart to EncryptWithPrefixIVToBase64 and is convenient for scenarios where the encrypted data is stored or transmitted as a Base64 string.
        /// </summary>
        /// <param name="base64Data">A Base64-encoded string containing the IV, ciphertext, and (for GCM mode) authentication tag.</param>
        /// <param name="associatedData">Optional additional data that was authenticated in GCM mode. Ignored for non-GCM modes.</param>
        /// <returns>The decrypted plaintext data.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when base64Data is null.</exception>
        /// <exception cref="FormatException">Thrown when base64Data is not a valid Base64 string.</exception>
        /// <exception cref="ArgumentException">Thrown when the decoded data has insufficient length for the expected format.</exception>
        /// <exception cref="CryptographicException">Thrown when the authentication tag verification fails in GCM mode or when the decryption operation fails.</exception>
        public byte[] DecryptWithPrefixIVFromBase64(string base64Data, byte[]? associatedData = null)
        {
            byte[] combined = Convert.FromBase64String(base64Data);
            return DecryptWithPrefixIV(combined, associatedData);
        }

        #endregion

        #region 遗留方法

        /// <summary>
        /// Encrypts the specified plaintext data using ECB mode without requiring an initialization vector (IV).
        /// This method is marked as obsolete because ECB mode is insecure and should not be used for new applications.
        /// It is provided only for legacy compatibility with existing systems that require ECB mode.
        /// ECB mode does not use an IV, so identical plaintext blocks produce identical ciphertext blocks,
        /// which can leak information about the plaintext structure.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <returns>The encrypted ciphertext data.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when plaintext is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when this method is called in a mode other than ECB.</exception>
        [Obsolete("ECB mode is insecure. Use only for legacy compatibility.")]
        public byte[] EncryptWithoutIV_ECB(byte[] plaintext)
        {
            if (_extendedMode != ExtendedCipherMode.ECB)
                throw new InvalidOperationException("This method is only valid in ECB mode");

            return Encrypt(plaintext, new byte[AES_IV_SIZE]); // 使用全零IV
        }

        /// <summary>
        /// Decrypts the specified ciphertext data using ECB mode without requiring an initialization vector (IV).
        /// This method is marked as obsolete because ECB mode is insecure and should not be used for new applications.
        /// It is provided only for legacy compatibility with existing systems that require ECB mode.
        /// ECB mode does not use an IV, so identical plaintext blocks produce identical ciphertext blocks,
        /// which can leak information about the plaintext structure.
        /// </summary>
        /// <param name="ciphertext">The ciphertext data to decrypt.</param>
        /// <returns>The decrypted plaintext data.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when ciphertext is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when this method is called in a mode other than ECB.</exception>
        [Obsolete("ECB mode is insecure. Use only for legacy compatibility.")]
        public byte[] DecryptWithoutIV_ECB(byte[] ciphertext)
        {
            if (_extendedMode != ExtendedCipherMode.ECB)
                throw new InvalidOperationException("This method is only valid in ECB mode");

            return Decrypt(ciphertext, new byte[AES_IV_SIZE]); // 使用全零IV
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// Converts a hexadecimal string to a byte array.
        /// This method is useful for converting hexadecimal-encoded data to its binary representation.
        /// </summary>
        /// <param name="hex">The hexadecimal string to convert. Must have an even length.</param>
        /// <returns>A byte array representing the hexadecimal string.</returns>
        /// <exception cref="ArgumentException">Thrown when the hexadecimal string has an odd length or contains invalid characters.</exception>
        private static byte[] HexStringToBytes(string hex)
        {
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have even length");

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// Converts a byte array to a lowercase hexadecimal string.
        /// This method is useful for converting binary data to a hexadecimal representation for storage or transmission.
        /// </summary>
        /// <param name="bytes">The byte array to convert.</param>
        /// <returns>A lowercase hexadecimal string representing the byte array.</returns>
        private static string BytesToHexString(byte[] bytes)
        {
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>
        /// Validates that the specified key is a valid AES key.
        /// AES supports key sizes of 128, 192, or 256 bits (16, 24, or 32 bytes).
        /// </summary>
        /// <param name="key">The key to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when the key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the key length is not 16, 24, or 32 bytes.</exception>
        private static void ValidateKey(byte[] key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException("AES key must be 128, 192, or 256 bits (16, 24, or 32 bytes)");
        }

        /// <summary>
        /// Validates that the specified cipher mode and padding mode are compatible.
        /// This method ensures that the combination of cipher mode and padding mode is valid.
        /// </summary>
        /// <param name="mode">The cipher mode to validate.</param>
        /// <param name="padding">The padding mode to validate.</param>
        /// <exception cref="ArgumentException">Thrown when the cipher mode or padding mode is invalid, or when the combination is not supported.</exception>
        private static void ValidateModeAndPadding(ExtendedCipherMode mode, PaddingMode padding)
        {
            if (!Enum.IsDefined(typeof(ExtendedCipherMode), mode))
                throw new ArgumentException($"Invalid cipher mode: {mode}");

            if (!Enum.IsDefined(typeof(PaddingMode), padding))
                throw new ArgumentException($"Invalid padding mode: {padding}");

            // GCM模式只支持NoPadding
            if (mode == ExtendedCipherMode.GCM && padding != PaddingMode.None)
                throw new ArgumentException("GCM mode only supports PaddingMode.None");
        }

        /// <summary>
        /// Validates that the specified initialization vector (IV) has the correct length for AES.
        /// AES requires an IV of 16 bytes (128 bits).
        /// </summary>
        /// <param name="iv">The initialization vector (IV) to validate.</param>
        /// <exception cref="ArgumentException">Thrown when the IV is not 16 bytes in length.</exception>
        private void ValidateIV(ReadOnlySpan<byte> iv)
        {
            if (_isGcmMode)
            {
                // GCM 模式推荐使用 12 字节 IV
                if (iv.Length != 12 && iv.Length != AES_IV_SIZE)
                    throw new ArgumentException($"IV must be 12 or 16 bytes for AES-GCM");
            }
            else
            {
                // 非 GCM 模式使用 16 字节 IV
                if (iv.Length != AES_IV_SIZE)
                    throw new ArgumentException($"IV must be {AES_IV_SIZE} bytes for AES");
            }
        }

        /// <summary>
        /// Throws an ObjectDisposedException if this instance has been disposed.
        /// This method is marked as aggressive inline for performance optimization.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        #endregion

        #region 清理和资源释放

        /// <summary>
        /// Releases all resources used by the current instance of the AesCrypto class.
        /// This method clears the cryptographic key from memory and disposes of the underlying cryptographic providers.
        /// After calling this method, the instance cannot be used for further cryptographic operations.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            ClearKey();

            _aes?.Dispose();
            _aesGcm?.Dispose();

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Clears the cryptographic key from memory by overwriting it with zeros.
        /// This method uses unsafe code to ensure the key is securely removed from memory.
        /// After calling this method, the key cannot be recovered and the instance cannot be used for further cryptographic operations.
        /// </summary>
        public void ClearKey()
        {
            if (_key != null)
            {
                // 使用不安全代码将密钥内存清零
                unsafe
                {
                    fixed (byte* pKey = _key)
                    {
                        Unsafe.InitBlock(pKey, 0, (uint)_key.Length);
                    }
                }
                _key = EmptyByteArray;
            }
        }

        /// <summary>
        /// Determines whether the specified cipher mode is supported on the current platform.
        /// This method performs runtime capability checking for different AES encryption modes.
        /// For GCM mode, it verifies that the underlying platform supports AES-GCM authenticated encryption.
        /// For other modes (CBC, ECB, CFB), it verifies that the AES implementation supports the requested mode.
        /// </summary>
        /// <param name="mode">The cipher mode to check for support. Use values from the <see cref="ExtendedCipherMode"/> enumeration.</param>
        /// <returns>
        /// <c>true</c> if the specified cipher mode is supported on the current platform; otherwise, <c>false</c>.
        /// Returns <c>false</c> for unsupported modes or when platform restrictions prevent the mode from being used.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is particularly useful for checking GCM support, as AES-GCM is not available on all platforms.
        /// For example, some older .NET Framework versions or certain platforms (like WebAssembly) may not support GCM.
        /// </para>
        /// <para>
        /// The method uses defensive exception handling to determine support. If creating an instance
        /// of the required cryptographic provider throws an exception, the mode is considered unsupported.
        /// This approach ensures compatibility across different runtime environments.
        /// </para>
        /// <para>
        /// <strong>Performance Note:</strong> This method creates temporary cryptographic objects
        /// to test support. While generally lightweight, it's recommended to call this method once
        /// and cache the result rather than calling it repeatedly in performance-critical code.
        /// </para>
        /// <example>
        /// The following example demonstrates how to use this method to safely choose an encryption mode:
        /// <code>
        /// AesCrypto.ExtendedCipherMode preferredMode = AesCrypto.ExtendedCipherMode.GCM;
        /// 
        /// if (AesCrypto.IsModeSupported(preferredMode))
        /// {
        ///     // Use authenticated encryption with GCM
        ///     using var aes = new AesCrypto(key, AesCrypto.ExtendedCipherMode.GCM, PaddingMode.None);
        /// }
        /// else
        /// {
        ///     // Fall back to CBC mode
        ///     using var aes = new AesCrypto(key, AesCrypto.ExtendedCipherMode.CBC);
        /// }
        /// </code>
        /// </example>
        /// </remarks>
        /// <seealso cref="ExtendedCipherMode"/>
        /// <seealso cref="AesCrypto(byte[], ExtendedCipherMode, PaddingMode)"/>
        /// <exception cref="System.Security.Cryptography.CryptographicException">
        /// Thrown when a cryptographic operation fails during the capability check.
        /// However, this method catches and handles such exceptions internally.
        /// </exception>
        public static bool IsModeSupported(ExtendedCipherMode mode)
        {
            if (mode == ExtendedCipherMode.GCM)
            {
                // Check GCM support
                try
                {
#pragma warning disable SYSLIB0053 // Type or member is obsolete
                    using var aesGcm = new AesGcm(new byte[16]);
#pragma warning restore SYSLIB0053 // Type or member is obsolete
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            try
            {
                using var aes = Aes.Create();
                CipherMode standardMode = mode switch
                {
                    ExtendedCipherMode.CBC => CipherMode.CBC,
                    ExtendedCipherMode.ECB => CipherMode.ECB,
                    ExtendedCipherMode.CFB => CipherMode.CFB,
                    _ => CipherMode.CBC
                };

                aes.Mode = standardMode;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Finalizer for the AesCrypto class.
        /// This method is called by the garbage collector to ensure resources are released
        /// when the instance is not properly disposed.
        /// It is recommended to call Dispose explicitly when finished with the instance
        /// rather than relying on the finalizer.
        /// </summary>
        ~AesCrypto()
        {
            Dispose();
        }

        #endregion
#nullable restore
    }
}
#endif
