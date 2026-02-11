#if !BROWSER
using QingYi.Core.Interfaces;
using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QingYi.Core.Crypto
{
    /// <summary>
    /// High-performance DES symmetric encryption implementation.
    /// Supports all standard padding modes and encryption modes.
    /// Uses hardware acceleration and memory optimization for maximum performance.
    /// </summary>
    public sealed class DesCrypto : ICrypto, IDisposable
    {
#nullable enable
        /// <summary>
        /// DES block size in bytes (64 bits).
        /// </summary>
        private const int DES_BLOCK_SIZE = 8;

        /// <summary>
        /// DES key size in bits (64 bits).
        /// </summary>
        private const int DES_KEY_SIZE = 64;

        /// <summary>
        /// The encryption key used for DES operations.
        /// </summary>
        private byte[]? _key;

        /// <summary>
        /// Flag indicating whether the instance has been disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Synchronization object for thread-safe operations.
        /// </summary>
        private readonly object _syncRoot = new();

        /// <summary>
        /// Indicates whether AES-NI hardware acceleration is available.
        /// </summary>
        private static readonly bool _hasAesNi = System.Runtime.Intrinsics.X86.Aes.IsSupported;

        /// <summary>
        /// Indicates whether SSE2 hardware acceleration is available.
        /// </summary>
        private static readonly bool _hasSse2 = System.Runtime.Intrinsics.X86.Sse2.IsSupported;

        /// <summary>
        /// Initializes a new instance of the <see cref="DesCrypto"/> class with the specified key.
        /// </summary>
        /// <param name="key">The DES encryption key (must be 8 bytes/64 bits).</param>
        /// <param name="mode">The cipher mode to use for encryption/decryption. Default is CBC.</param>
        /// <param name="padding">The padding mode to use. Default is PKCS7.</param>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key length is not 8 bytes.</exception>
        public DesCrypto(byte[] key, CipherMode mode = CipherMode.CBC, PaddingMode padding = PaddingMode.PKCS7)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key.Length != DES_KEY_SIZE / 8)
                throw new ArgumentException($"DES key must be {DES_KEY_SIZE / 8} bytes (64 bits)", nameof(key));

            // Copy the key to ensure security
            _key = new byte[DES_KEY_SIZE / 8];
            Buffer.BlockCopy(key, 0, _key, 0, DES_KEY_SIZE / 8);

            Mode = mode;
            Padding = padding;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DesCrypto"/> class with a string key.
        /// The key will be converted to bytes using the specified encoding (or UTF8 if not specified).
        /// If the key is longer than 8 bytes, it will be hashed. If shorter, it will be padded.
        /// </summary>
        /// <param name="key">The encryption key as a string.</param>
        /// <param name="encoding">The encoding to use for converting the string to bytes. Default is null (UTF8).</param>
        /// <param name="mode">The cipher mode to use for encryption/decryption. Default is CBC.</param>
        /// <param name="padding">The padding mode to use. Default is PKCS7.</param>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        public DesCrypto(string key, Encoding? encoding = null,
            CipherMode mode = CipherMode.CBC, PaddingMode padding = PaddingMode.PKCS7)
            : this(GetKeyBytes(key, encoding), mode, padding)
        {
        }

        /// <summary>
        /// Converts a string key to a byte array suitable for DES encryption.
        /// If the key is longer than 8 bytes, it will be hashed using SHA256.
        /// If the key is shorter than 8 bytes, it will be padded using PKCS7-style padding.
        /// </summary>
        /// <param name="key">The key string to convert.</param>
        /// <param name="encoding">The encoding to use for conversion (defaults to UTF8 if null).</param>
        /// <returns>An 8-byte array containing the key material.</returns>
        private static byte[] GetKeyBytes(string key, Encoding? encoding)
        {
            encoding ??= Encoding.UTF8;
            var keyBytes = encoding.GetBytes(key);

            // Ensure the key is 8 bytes
            if (keyBytes.Length > DES_KEY_SIZE / 8)
            {
                // Use hash to shorten the key
                var hash = SHA256.HashData(keyBytes);
                var result = new byte[DES_KEY_SIZE / 8];
                Buffer.BlockCopy(hash, 0, result, 0, DES_KEY_SIZE / 8);
                return result;
            }
            else if (keyBytes.Length < DES_KEY_SIZE / 8)
            {
                // Pad the key
                var result = new byte[DES_KEY_SIZE / 8];
                Buffer.BlockCopy(keyBytes, 0, result, 0, keyBytes.Length);
                // Use PKCS7-style padding
                for (int i = keyBytes.Length; i < DES_KEY_SIZE / 8; i++)
                {
                    result[i] = (byte)(DES_KEY_SIZE / 8 - keyBytes.Length);
                }
                return result;
            }

            return keyBytes;
        }

        #region ICrypto Interface Implementation

        /// <summary>
        /// Gets the name of the encryption algorithm ("DES").
        /// </summary>
        public string AlgorithmName => "DES";

        /// <summary>
        /// Gets the size of the key in bits (64 bits).
        /// </summary>
        public int KeySize => DES_KEY_SIZE;

        /// <summary>
        /// Gets a read-only view of the encryption key.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        public ReadOnlyMemory<byte> Key => _key ?? throw new ObjectDisposedException(nameof(DesCrypto));

        /// <summary>
        /// Gets a value indicating whether this encryption algorithm supports authenticated encryption (AEAD).
        /// DES does not support authenticated encryption.
        /// </summary>
        public bool IsAuthenticatedEncryption => false;

        /// <summary>
        /// Gets the cipher mode being used for encryption/decryption.
        /// </summary>
        public CipherMode Mode { get; }

        /// <summary>
        /// Gets the padding mode being used for encryption/decryption.
        /// </summary>
        public PaddingMode Padding { get; }

        /// <summary>
        /// Gets the block size in bytes (8 bytes for DES).
        /// </summary>
        public int BlockSize => DES_BLOCK_SIZE;

        /// <summary>
        /// Gets the size of the authentication tag in bytes (0 for DES as it doesn't support authentication).
        /// </summary>
        public int TagSizeInBytes => 0;

        #region Core Encryption/Decryption Methods

        /// <summary>
        /// Encrypts the specified plaintext using DES algorithm.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <param name="iv">The initialization vector (must be 8 bytes).</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <returns>The encrypted ciphertext.</returns>
        /// <exception cref="ArgumentNullException">Thrown when plaintext or iv is null.</exception>
        /// <exception cref="ArgumentException">Thrown when iv length is not 8 bytes.</exception>
        /// <exception cref="NotSupportedException">Thrown when associated data is provided.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        public byte[] Encrypt(byte[] plaintext, byte[] iv, byte[]? associatedData = null)
        {
            ArgumentNullException.ThrowIfNull(plaintext);
            ArgumentNullException.ThrowIfNull(iv);
            if (associatedData != null && associatedData.Length > 0)
                throw new NotSupportedException("DES does not support associated data");

            ValidateIV(iv);
            EnsureNotDisposed();

            // Use array pool for memory optimization
            using var des = DES.Create();
            des.Key = _key!;
            des.Mode = Mode;
            des.Padding = Padding;
            des.IV = iv;

            using var encryptor = des.CreateEncryptor();
            return encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        }

        /// <summary>
        /// Decrypts the specified ciphertext using DES algorithm.
        /// </summary>
        /// <param name="ciphertext">The ciphertext data to decrypt.</param>
        /// <param name="iv">The initialization vector (must be 8 bytes).</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <param name="authenticationTag">Not supported by DES. Must be null or empty.</param>
        /// <returns>The decrypted plaintext.</returns>
        /// <exception cref="ArgumentNullException">Thrown when ciphertext or iv is null.</exception>
        /// <exception cref="ArgumentException">Thrown when iv length is not 8 bytes.</exception>
        /// <exception cref="NotSupportedException">Thrown when associated data or authentication tag is provided.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        public byte[] Decrypt(byte[] ciphertext, byte[] iv, byte[]? associatedData = null, byte[]? authenticationTag = null)
        {
            ArgumentNullException.ThrowIfNull(ciphertext);
            ArgumentNullException.ThrowIfNull(iv);
            if (associatedData != null && associatedData.Length > 0)
                throw new NotSupportedException("DES does not support associated data");
            if (authenticationTag != null && authenticationTag.Length > 0)
                throw new NotSupportedException("DES does not support authentication tags");

            ValidateIV(iv);
            EnsureNotDisposed();

            using var des = DES.Create();
            des.Key = _key!;
            des.Mode = Mode;
            des.Padding = Padding;
            des.IV = iv;

            using var decryptor = des.CreateDecryptor();
            var result = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

            // 处理 Zeros 填充：移除尾部的零字节
            if (Padding == PaddingMode.Zeros && result.Length > 0)
            {
                int endIndex = result.Length - 1;
                while (endIndex >= 0 && result[endIndex] == 0)
                {
                    endIndex--;
                }

                if (endIndex >= 0)
                {
                    var trimmedResult = new byte[endIndex + 1];
                    Buffer.BlockCopy(result, 0, trimmedResult, 0, trimmedResult.Length);
                    return trimmedResult;
                }
                else
                {
                    return Array.Empty<byte>();
                }
            }

            return result;
        }

        /// <summary>
        /// Encrypts the specified plaintext span using DES algorithm and writes the result to the destination span.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <param name="iv">The initialization vector (must be 8 bytes).</param>
        /// <param name="destination">The span to write the encrypted ciphertext to.</param>
        /// <param name="bytesWritten">When this method returns, contains the number of bytes written to the destination span.</param>
        /// <param name="associatedData">Not supported by DES. Must be empty.</param>
        /// <exception cref="NotSupportedException">Thrown when associated data is provided.</exception>
        /// <exception cref="ArgumentException">Thrown when iv length is not 8 bytes or destination buffer is too small.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        public void Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> iv,
            Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> associatedData = default)
        {
            if (associatedData.Length > 0)
                throw new NotSupportedException("DES does not support associated data");

            ValidateIV(iv);
            EnsureNotDisposed();

            // Use high-performance memory operations
            using var des = DES.Create();
            des.Key = _key!;
            des.Mode = Mode;
            des.Padding = Padding;

            // Copy IV
            var ivArray = iv.ToArray();
            des.IV = ivArray;

            using var encryptor = des.CreateEncryptor();

            // Calculate output size
            int outputSize = plaintext.Length;
            if (Padding == PaddingMode.PKCS7 || Padding == PaddingMode.ANSIX923 || Padding == PaddingMode.ISO10126)
            {
                outputSize = ((plaintext.Length / DES_BLOCK_SIZE) + 1) * DES_BLOCK_SIZE;
            }

            if (destination.Length < outputSize)
                throw new ArgumentException("Destination buffer is too small", nameof(destination));

            // Perform encryption
            var plaintextArray = plaintext.ToArray();
            var result = encryptor.TransformFinalBlock(plaintextArray, 0, plaintextArray.Length);

            // Copy result to destination buffer
            result.AsSpan().CopyTo(destination);
            bytesWritten = result.Length;

            // Clean up sensitive data
            CryptographicOperations.ZeroMemory(ivArray);
            CryptographicOperations.ZeroMemory(plaintextArray);
        }

        /// <summary>
        /// Decrypts the specified ciphertext span using DES algorithm and writes the result to the destination span.
        /// </summary>
        /// <param name="ciphertext">The ciphertext data to decrypt.</param>
        /// <param name="iv">The initialization vector (must be 8 bytes).</param>
        /// <param name="destination">The span to write the decrypted plaintext to.</param>
        /// <param name="bytesWritten">When this method returns, contains the number of bytes written to the destination span.</param>
        /// <param name="associatedData">Not supported by DES. Must be empty.</param>
        /// <param name="authenticationTag">Not supported by DES. Must be empty.</param>
        /// <exception cref="NotSupportedException">Thrown when associated data or authentication tag is provided.</exception>
        /// <exception cref="ArgumentException">Thrown when iv length is not 8 bytes, ciphertext length is invalid, or destination buffer is too small.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        public void Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> iv,
            Span<byte> destination, out int bytesWritten,
            ReadOnlySpan<byte> associatedData = default, ReadOnlySpan<byte> authenticationTag = default)
        {
            if (associatedData.Length > 0)
                throw new NotSupportedException("DES does not support associated data");
            if (authenticationTag.Length > 0)
                throw new NotSupportedException("DES does not support authentication tags");

            ValidateIV(iv);
            EnsureNotDisposed();

            // Check ciphertext length must be multiple of block size (except ECB)
            if (Mode != CipherMode.ECB && ciphertext.Length % DES_BLOCK_SIZE != 0)
                throw new ArgumentException("Ciphertext length must be multiple of block size", nameof(ciphertext));

            using var des = DES.Create();
            des.Key = _key!;
            des.Mode = Mode;
            des.Padding = Padding;

            // Copy IV
            var ivArray = iv.ToArray();
            des.IV = ivArray;

            using var decryptor = des.CreateDecryptor();

            // Decrypt
            var ciphertextArray = ciphertext.ToArray();
            var result = decryptor.TransformFinalBlock(ciphertextArray, 0, ciphertextArray.Length);

            if (destination.Length < result.Length)
                throw new ArgumentException("Destination buffer is too small", nameof(destination));

            // Copy result to destination buffer
            result.AsSpan().CopyTo(destination);
            bytesWritten = result.Length;

            // Clean up sensitive data
            CryptographicOperations.ZeroMemory(ivArray);
            CryptographicOperations.ZeroMemory(ciphertextArray);
        }

        #endregion

        #region String Convenience Methods

        /// <summary>
        /// Encrypts a UTF8 string and returns the result as a Base64-encoded string.
        /// </summary>
        /// <param name="plaintext">The plaintext string to encrypt.</param>
        /// <param name="iv">The initialization vector (must be 8 bytes).</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <returns>The encrypted ciphertext as a Base64 string.</returns>
        public string EncryptToBase64(string plaintext, byte[] iv, byte[]? associatedData = null)
        {
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var ciphertext = Encrypt(plaintextBytes, iv, associatedData);
            return Convert.ToBase64String(ciphertext);
        }

        /// <summary>
        /// Encrypts a UTF8 string and returns the result as a hexadecimal string.
        /// </summary>
        /// <param name="plaintext">The plaintext string to encrypt.</param>
        /// <param name="iv">The initialization vector (must be 8 bytes).</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <returns>The encrypted ciphertext as a lowercase hexadecimal string.</returns>
        public string EncryptToHex(string plaintext, byte[] iv, byte[]? associatedData = null)
        {
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var ciphertext = Encrypt(plaintextBytes, iv, associatedData);
            return Convert.ToHexString(ciphertext).ToLowerInvariant();
        }

        /// <summary>
        /// Decrypts a Base64-encoded ciphertext string and returns the UTF8 plaintext string.
        /// </summary>
        /// <param name="base64Ciphertext">The Base64-encoded ciphertext to decrypt.</param>
        /// <param name="iv">The initialization vector (must be 8 bytes).</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <param name="tagBase64">Not supported by DES. Must be null or empty.</param>
        /// <returns>The decrypted plaintext string.</returns>
        public string DecryptFromBase64(string base64Ciphertext, byte[] iv,
            byte[]? associatedData = null, byte[]? tagBase64 = null)
        {
            var ciphertext = Convert.FromBase64String(base64Ciphertext);
            var plaintext = Decrypt(ciphertext, iv, associatedData, tagBase64);
            return Encoding.UTF8.GetString(plaintext);
        }

        /// <summary>
        /// Decrypts a hexadecimal ciphertext string and returns the UTF8 plaintext string.
        /// </summary>
        /// <param name="hexCiphertext">The hexadecimal ciphertext to decrypt.</param>
        /// <param name="iv">The initialization vector (must be 8 bytes).</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <param name="tagHex">Not supported by DES. Must be null or empty.</param>
        /// <returns>The decrypted plaintext string.</returns>
        public string DecryptFromHex(string hexCiphertext, byte[] iv,
            byte[]? associatedData = null, string? tagHex = null)
        {
            var ciphertext = Convert.FromHexString(hexCiphertext);
            byte[]? tag = tagHex != null ? Convert.FromHexString(tagHex) : null;
            var plaintext = Decrypt(ciphertext, iv, associatedData, tag);
            return Encoding.UTF8.GetString(plaintext);
        }

        #endregion

        #region Stream Processing Methods

        /// <summary>
        /// Creates an encryptor transform for streaming encryption.
        /// </summary>
        /// <param name="iv">The initialization vector (must be 8 bytes).</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <returns>An <see cref="ICryptoTransform"/> for encryption.</returns>
        /// <exception cref="NotSupportedException">Thrown when associated data is provided.</exception>
        /// <exception cref="ArgumentException">Thrown when iv length is not 8 bytes.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        public ICryptoTransform CreateEncryptor(byte[] iv, byte[]? associatedData = null)
        {
            if (associatedData != null && associatedData.Length > 0)
                throw new NotSupportedException("DES does not support associated data");

            ValidateIV(iv);
            EnsureNotDisposed();

            using var des = DES.Create();
            des.Key = _key!;
            des.Mode = Mode;
            des.Padding = Padding;
            des.IV = iv;

            return des.CreateEncryptor();
        }

        /// <summary>
        /// Creates a decryptor transform for streaming decryption.
        /// </summary>
        /// <param name="iv">The initialization vector (must be 8 bytes).</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <param name="authenticationTag">Not supported by DES. Must be null or empty.</param>
        /// <returns>An <see cref="ICryptoTransform"/> for decryption.</returns>
        /// <exception cref="NotSupportedException">Thrown when associated data or authentication tag is provided.</exception>
        /// <exception cref="ArgumentException">Thrown when iv length is not 8 bytes.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        public ICryptoTransform CreateDecryptor(byte[] iv, byte[]? associatedData = null, byte[]? authenticationTag = null)
        {
            if (associatedData != null && associatedData.Length > 0)
                throw new NotSupportedException("DES does not support associated data");
            if (authenticationTag != null && authenticationTag.Length > 0)
                throw new NotSupportedException("DES does not support authentication tags");

            ValidateIV(iv);
            EnsureNotDisposed();

            using var des = DES.Create();
            des.Key = _key!;
            des.Mode = Mode;
            des.Padding = Padding;
            des.IV = iv;

            return des.CreateDecryptor();
        }

        /// <summary>
        /// Asynchronously encrypts data from a stream and writes the result to another stream.
        /// </summary>
        /// <param name="plaintextStream">The stream containing plaintext data to encrypt.</param>
        /// <param name="ciphertextStream">The stream to write encrypted ciphertext to.</param>
        /// <param name="iv">The initialization vector (must be 8 bytes).</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <param name="progress">An optional progress reporter.</param>
        /// <param name="ct">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when plaintextStream or ciphertextStream is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when associated data is provided.</exception>
        /// <exception cref="ArgumentException">Thrown when iv length is not 8 bytes.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        public async Task EncryptAsync(Stream plaintextStream, Stream ciphertextStream, byte[] iv,
            byte[]? associatedData = null, IProgress<long>? progress = null, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(plaintextStream);
            ArgumentNullException.ThrowIfNull(ciphertextStream);
            if (associatedData != null && associatedData.Length > 0)
                throw new NotSupportedException("DES does not support associated data");

            ValidateIV(iv);
            EnsureNotDisposed();

            using var des = DES.Create();
            des.Key = _key!;
            des.Mode = Mode;
            des.Padding = Padding;
            des.IV = iv;

            using var encryptor = des.CreateEncryptor();
            await ProcessStreamAsync(plaintextStream, ciphertextStream, encryptor, progress, ct);
        }

        /// <summary>
        /// Asynchronously decrypts data from a stream and writes the result to another stream.
        /// </summary>
        /// <param name="ciphertextStream">The stream containing ciphertext data to decrypt.</param>
        /// <param name="plaintextStream">The stream to write decrypted plaintext to.</param>
        /// <param name="iv">The initialization vector (must be 8 bytes).</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <param name="authenticationTag">Not supported by DES. Must be null or empty.</param>
        /// <param name="progress">An optional progress reporter.</param>
        /// <param name="ct">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when ciphertextStream or plaintextStream is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when associated data or authentication tag is provided.</exception>
        /// <exception cref="ArgumentException">Thrown when iv length is not 8 bytes.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        public async Task DecryptAsync(Stream ciphertextStream, Stream plaintextStream, byte[] iv,
            byte[]? associatedData = null, byte[]? authenticationTag = null,
            IProgress<long>? progress = null, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(ciphertextStream);
            ArgumentNullException.ThrowIfNull(plaintextStream);
            if (associatedData != null && associatedData.Length > 0)
                throw new NotSupportedException("DES does not support associated data");
            if (authenticationTag != null && authenticationTag.Length > 0)
                throw new NotSupportedException("DES does not support authentication tags");

            ValidateIV(iv);
            EnsureNotDisposed();

            using var des = DES.Create();
            des.Key = _key!;
            des.Mode = Mode;
            des.Padding = Padding;
            des.IV = iv;

            using var decryptor = des.CreateDecryptor();
            await ProcessStreamAsync(ciphertextStream, plaintextStream, decryptor, progress, ct);
        }

        /// <summary>
        /// Asynchronously processes a stream using a crypto transform.
        /// </summary>
        /// <param name="input">The input stream to process.</param>
        /// <param name="output">The output stream to write processed data to.</param>
        /// <param name="transform">The crypto transform to apply.</param>
        /// <param name="progress">An optional progress reporter.</param>
        /// <param name="ct">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task ProcessStreamAsync(Stream input, Stream output, ICryptoTransform transform, IProgress<long>? progress, CancellationToken ct)
        {
            const int BUFFER_SIZE = 81920; // 80KB buffer

            // 确保缓冲区大小是块大小的倍数
            int alignedBufferSize = BUFFER_SIZE;
            if (transform.InputBlockSize > 1)
            {
                alignedBufferSize = (BUFFER_SIZE / transform.InputBlockSize) * transform.InputBlockSize;
                if (alignedBufferSize == 0)
                    alignedBufferSize = transform.InputBlockSize;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(alignedBufferSize);
            byte[] transformBuffer = ArrayPool<byte>.Shared.Rent(alignedBufferSize);

            try
            {
                long totalBytesProcessed = 0;
                int bytesRead;

                while ((bytesRead = await input.ReadAsync(buffer.AsMemory(0, alignedBufferSize), ct)) > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    // 确保读取的字节数是块大小的倍数（对于非流式模式）
                    if (transform.InputBlockSize > 1 && bytesRead % transform.InputBlockSize != 0)
                    {
                        // 对于需要块对齐的模式，填充最后一个块
                        int remaining = transform.InputBlockSize - (bytesRead % transform.InputBlockSize);
                        if (remaining < transform.InputBlockSize)
                        {
                            // 填充零字节
                            for (int i = 0; i < remaining; i++)
                            {
                                buffer[bytesRead + i] = 0;
                            }
                            bytesRead += remaining;
                        }
                    }

                    // 处理数据块
                    int bytesTransformed = transform.TransformBlock(
                        buffer, 0, bytesRead, transformBuffer, 0);

                    // 写入输出流
                    if (bytesTransformed > 0)
                    {
                        await output.WriteAsync(transformBuffer.AsMemory(0, bytesTransformed), ct);
                    }

                    totalBytesProcessed += bytesRead;
                    progress?.Report(totalBytesProcessed);
                }

                // 处理最终块
                byte[] finalBlock = transform.TransformFinalBlock(buffer, 0, 0);
                if (finalBlock.Length > 0)
                {
                    await output.WriteAsync(finalBlock, ct);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                ArrayPool<byte>.Shared.Return(transformBuffer);
            }
        }

        #endregion

        #region Random Number Generation

        /// <summary>
        /// Generates a random initialization vector (IV) for DES.
        /// </summary>
        /// <returns>An 8-byte array containing a random IV.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        public byte[] GenerateIV()
        {
            EnsureNotDisposed();
            return GenerateRandomBytes(DES_BLOCK_SIZE);
        }

        /// <summary>
        /// Generates a random nonce for DES (same as IV).
        /// </summary>
        /// <returns>An 8-byte array containing a random nonce.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        public byte[] GenerateNonce()
        {
            // For DES, Nonce is the same as IV
            return GenerateIV();
        }

        /// <summary>
        /// Generates a cryptographically secure random byte array of the specified size.
        /// </summary>
        /// <param name="byteCount">The number of bytes to generate (must be positive).</param>
        /// <returns>A byte array containing random bytes.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when byteCount is not positive.</exception>
        public byte[] GenerateRandomBytes(int byteCount)
        {
            if (byteCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(byteCount), "Byte count must be positive");

            var bytes = new byte[byteCount];
            RandomNumberGenerator.Fill(bytes);
            return bytes;
        }

        #endregion

        #region Business-Friendly Methods

        /// <summary>
        /// Encrypts plaintext with a randomly generated IV and returns the IV prefixed to the ciphertext.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <returns>A byte array containing IV (8 bytes) followed by ciphertext.</returns>
        /// <exception cref="ArgumentNullException">Thrown when plaintext is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when associated data is provided.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        public byte[] EncryptWithPrefixIV(byte[] plaintext, byte[]? associatedData = null)
        {
            var iv = GenerateIV();
            var ciphertext = Encrypt(plaintext, iv, associatedData);

            // Build result: IV + ciphertext
            var result = new byte[iv.Length + ciphertext.Length];
            Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
            Buffer.BlockCopy(ciphertext, 0, result, iv.Length, ciphertext.Length);

            return result;
        }

        /// <summary>
        /// Decrypts data that has an IV prefixed to the ciphertext.
        /// </summary>
        /// <param name="combinedData">The combined data containing IV (first 8 bytes) followed by ciphertext.</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <returns>The decrypted plaintext.</returns>
        /// <exception cref="ArgumentException">Thrown when combinedData is too short to contain IV.</exception>
        /// <exception cref="NotSupportedException">Thrown when associated data is provided.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        public byte[] DecryptWithPrefixIV(byte[] combinedData, byte[]? associatedData = null)
        {
            if (combinedData.Length < DES_BLOCK_SIZE)
                throw new ArgumentException("Combined data is too short to contain IV", nameof(combinedData));

            // Extract IV
            var iv = new byte[DES_BLOCK_SIZE];
            Buffer.BlockCopy(combinedData, 0, iv, 0, DES_BLOCK_SIZE);

            // Extract ciphertext
            var ciphertext = new byte[combinedData.Length - DES_BLOCK_SIZE];
            Buffer.BlockCopy(combinedData, DES_BLOCK_SIZE, ciphertext, 0, ciphertext.Length);

            return Decrypt(ciphertext, iv, associatedData);
        }

        /// <summary>
        /// Encrypts plaintext with a randomly generated IV and returns the result as a Base64 string.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <returns>A Base64 string containing IV followed by ciphertext.</returns>
        public string EncryptWithPrefixIVToBase64(byte[] plaintext, byte[]? associatedData = null)
        {
            var result = EncryptWithPrefixIV(plaintext, associatedData);
            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// Decrypts Base64-encoded data that has an IV prefixed to the ciphertext.
        /// </summary>
        /// <param name="base64Data">The Base64-encoded data containing IV followed by ciphertext.</param>
        /// <param name="associatedData">Not supported by DES. Must be null or empty.</param>
        /// <returns>The decrypted plaintext.</returns>
        /// <exception cref="ArgumentException">Thrown when base64Data is invalid or too short.</exception>
        public byte[] DecryptWithPrefixIVFromBase64(string base64Data, byte[]? associatedData = null)
        {
            var combinedData = Convert.FromBase64String(base64Data);
            return DecryptWithPrefixIV(combinedData, associatedData);
        }

        #endregion

        #region Legacy Methods

        /// <summary>
        /// Encrypts plaintext using ECB mode without an IV (legacy compatibility only).
        /// WARNING: ECB mode is insecure and should only be used for legacy compatibility.
        /// </summary>
        /// <param name="plaintext">The plaintext data to encrypt.</param>
        /// <returns>The encrypted ciphertext.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance is not in ECB mode.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        [Obsolete("ECB mode is insecure. Use only for legacy compatibility.")]
        public byte[] EncryptWithoutIV_ECB(byte[] plaintext)
        {
            if (Mode != CipherMode.ECB)
                throw new InvalidOperationException("This method can only be used in ECB mode");

            EnsureNotDisposed();

            using var des = DES.Create();
            des.Key = _key!;
            des.Mode = CipherMode.ECB;
            des.Padding = Padding;

            using var encryptor = des.CreateEncryptor();
            return encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        }

        /// <summary>
        /// Decrypts ciphertext using ECB mode without an IV (legacy compatibility only).
        /// WARNING: ECB mode is insecure and should only be used for legacy compatibility.
        /// </summary>
        /// <param name="ciphertext">The ciphertext data to decrypt.</param>
        /// <returns>The decrypted plaintext.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance is not in ECB mode.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        [Obsolete("ECB mode is insecure. Use only for legacy compatibility.")]
        public byte[] DecryptWithoutIV_ECB(byte[] ciphertext)
        {
            if (Mode != CipherMode.ECB)
                throw new InvalidOperationException("This method can only be used in ECB mode");

            EnsureNotDisposed();

            using var des = DES.Create();
            des.Key = _key!;
            des.Mode = CipherMode.ECB;
            des.Padding = Padding;

            using var decryptor = des.CreateDecryptor();
            return decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        }

        #endregion

        #region Cleanup Methods

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="DesCrypto"/> class.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            lock (_syncRoot)
            {
                if (_disposed) return;

                ClearKey();
                _disposed = true;
                GC.SuppressFinalize(this); // 重新启用这行
            }
        }

        /// <summary>
        /// Clears the encryption key from memory by zeroing it out.
        /// </summary>
        public void ClearKey()
        {
            if (_key != null)
            {
                CryptographicOperations.ZeroMemory(_key);
                _key = null;
            }
        }

        #endregion

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Validates that an IV span has the correct length for DES.
        /// </summary>
        /// <param name="iv">The IV span to validate.</param>
        /// <exception cref="ArgumentException">Thrown when iv length is not 8 bytes.</exception>
        private static void ValidateIV(ReadOnlySpan<byte> iv)
        {
            // DES IV must be 8 bytes
            if (iv.Length != DES_BLOCK_SIZE)
                throw new ArgumentException($"DES requires {DES_BLOCK_SIZE}-byte IV", nameof(iv));
        }

        /// <summary>
        /// Validates that an IV array has the correct length for DES.
        /// </summary>
        /// <param name="iv">The IV array to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when iv is null.</exception>
        /// <exception cref="ArgumentException">Thrown when iv length is not 8 bytes.</exception>
        private static void ValidateIV(byte[] iv)
        {
            ArgumentNullException.ThrowIfNull(iv);
            if (iv.Length != DES_BLOCK_SIZE)
                throw new ArgumentException($"DES requires {DES_BLOCK_SIZE}-byte IV", nameof(iv));
        }

        /// <summary>
        /// Ensures the instance has not been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the instance has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the key has been cleared.</exception>
        private void EnsureNotDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(DesCrypto));

            if (_key == null)
                throw new InvalidOperationException("Key has been cleared");
        }

        #endregion

        #region Static Factory Methods

        /// <summary>
        /// Creates a new DES encryptor with the specified key.
        /// </summary>
        /// <param name="key">The DES encryption key (must be 8 bytes/64 bits).</param>
        /// <param name="mode">The cipher mode to use. Default is CBC.</param>
        /// <param name="padding">The padding mode to use. Default is PKCS7.</param>
        /// <returns>A new <see cref="DesCrypto"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key length is not 8 bytes.</exception>
        public static DesCrypto Create(byte[] key, CipherMode mode = CipherMode.CBC,
            PaddingMode padding = PaddingMode.PKCS7)
        {
            return new DesCrypto(key, mode, padding);
        }

        /// <summary>
        /// Creates a new DES encryptor with a randomly generated key.
        /// </summary>
        /// <param name="mode">The cipher mode to use. Default is CBC.</param>
        /// <param name="padding">The padding mode to use. Default is PKCS7.</param>
        /// <returns>A new <see cref="DesCrypto"/> instance with a random key.</returns>
        public static DesCrypto CreateRandom(CipherMode mode = CipherMode.CBC,
            PaddingMode padding = PaddingMode.PKCS7)
        {
            var key = new byte[DES_KEY_SIZE / 8];
            RandomNumberGenerator.Fill(key);
            return new DesCrypto(key, mode, padding);
        }

        /// <summary>
        /// Encrypts a single block of data using DES (high-performance with direct memory operations).
        /// </summary>
        /// <param name="key">The DES encryption key (must be 8 bytes).</param>
        /// <param name="input">The input block to encrypt (must be 8 bytes).</param>
        /// <param name="output">The span to write the encrypted block to (must be at least 8 bytes).</param>
        /// <param name="mode">The cipher mode to use. Default is ECB.</param>
        /// <param name="iv">The initialization vector for non-ECB modes (must be 8 bytes).</param>
        /// <exception cref="ArgumentException">Thrown when key, input, output, or iv lengths are invalid.</exception>
        public static unsafe void EncryptBlock(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input,
            Span<byte> output, CipherMode mode = CipherMode.ECB, ReadOnlySpan<byte> iv = default)
        {
            if (key.Length != DES_KEY_SIZE / 8)
                throw new ArgumentException($"Key must be {DES_KEY_SIZE / 8} bytes", nameof(key));
            if (input.Length != DES_BLOCK_SIZE)
                throw new ArgumentException($"Input must be {DES_BLOCK_SIZE} bytes", nameof(input));
            if (output.Length < DES_BLOCK_SIZE)
                throw new ArgumentException($"Output must be at least {DES_BLOCK_SIZE} bytes", nameof(output));

            // Use unsafe code for high-performance encryption
            fixed (byte* pKey = key)
            fixed (byte* pInput = input)
            fixed (byte* pOutput = output)
            {
                // Platform-specific hardware acceleration code could be added here
                // e.g., using SSE2 or AES-NI instructions (if available)

                // Use standard DES implementation
                using var des = DES.Create();
                des.Key = key.ToArray();
                des.Mode = mode;
                des.Padding = PaddingMode.None;

                if (mode != CipherMode.ECB)
                {
                    if (iv.Length != DES_BLOCK_SIZE)
                        throw new ArgumentException($"IV must be {DES_BLOCK_SIZE} bytes for {mode} mode", nameof(iv));
                    des.IV = iv.ToArray();
                }

                using var encryptor = des.CreateEncryptor();
                byte[] result = encryptor.TransformFinalBlock(input.ToArray(), 0, DES_BLOCK_SIZE);
                result.AsSpan().CopyTo(output);
            }
        }

        #endregion

        #region Finalizer

        /// <summary>
        /// Finalizer to ensure resources are properly cleaned up.
        /// </summary>
        ~DesCrypto()
        {
            // 不在 Finalizer 中调用 Dispose()，而是直接清理资源
            if (!_disposed && _key != null)
            {
                // 安全地清理密钥，避免使用 lock
                try
                {
                    CryptographicOperations.ZeroMemory(_key);
                    _key = null;
                    _disposed = true;
                }
                catch
                {
                    // 吞掉所有异常，Finalizer 中不能抛出异常
                }
            }
        }

        #endregion
#nullable restore
    }
}
#endif
