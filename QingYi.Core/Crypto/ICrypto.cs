using System;
using System.IO;

namespace QingYi.Core.Crypto
{
    /// <summary>
    /// Provides cryptographic operations for encryption and decryption.
    /// </summary>
    public interface ICrypto : IDisposable
    {
        /// <summary>
        /// Gets or sets the symmetric key used for cryptographic operations.
        /// </summary>
        byte[] Key { get; set; }

        /// <summary>
        /// Gets or sets the initialization vector (IV) used for cryptographic operations.
        /// </summary>
        byte[] IV { get; set; }

        /// <summary>
        /// Encrypts the specified plaintext data.
        /// </summary>
        /// <param name="plainData">The plaintext data to encrypt.</param>
        /// <returns>The encrypted data as a byte array.</returns>
        byte[] Encrypt(byte[] plainData);

        /// <summary>
        /// Decrypts the specified encrypted data.
        /// </summary>
        /// <param name="encryptedData">The encrypted data to decrypt.</param>
        /// <returns>The decrypted plaintext data as a byte array.</returns>
        byte[] Decrypt(byte[] encryptedData);

        /// <summary>
        /// Encrypts data from the input stream and writes the result to the output stream.
        /// </summary>
        /// <param name="input">The stream containing plaintext data.</param>
        /// <param name="output">The stream to write encrypted data to.</param>
        void Encrypt(Stream input, Stream output);

        /// <summary>
        /// Decrypts data from the input stream and writes the result to the output stream.
        /// </summary>
        /// <param name="input">The stream containing encrypted data.</param>
        /// <param name="output">The stream to write decrypted data to.</param>
        void Decrypt(Stream input, Stream output);

        /// <summary>
        /// Generates a new random key and initialization vector (IV).
        /// </summary>
        void GenerateKeyIV();

        // Conditionally compiled methods for Span support
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        /// <summary>
        /// Encrypts data from a read-only span buffer.
        /// </summary>
        /// <param name="source">The read-only span containing plaintext data.</param>
        /// <returns>The encrypted data as a byte array.</returns>
        byte[] Encrypt(ReadOnlySpan<byte> source);

        /// <summary>
        /// Decrypts data from a read-only span buffer.
        /// </summary>
        /// <param name="source">The read-only span containing encrypted data.</param>
        /// <returns>The decrypted plaintext data as a byte array.</returns>
        byte[] Decrypt(ReadOnlySpan<byte> source);
#endif
    }
}