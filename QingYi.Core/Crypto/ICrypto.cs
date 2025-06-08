using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QingYi.Core.Crypto
{
    public interface ICrypto : IDisposable
    {
        byte[] Key { get; set; }
        byte[] IV { get; set; }

        // 基本字节数组操作
        byte[] Encrypt(byte[] plainData);
        byte[] Decrypt(byte[] encryptedData);

        // 流操作
        void Encrypt(Stream input, Stream output);
        void Decrypt(Stream input, Stream output);

        // 内存高效操作
        void GenerateKeyIV();

        // 条件编译支持Span
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        byte[] Encrypt(ReadOnlySpan<byte> source);
        byte[] Decrypt(ReadOnlySpan<byte> source);
#endif
    }
}
