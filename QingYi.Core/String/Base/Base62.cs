using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace QingYi.Core.String.Base
{
    public class Base62
    {
        private const string Characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private static readonly Encoding s_latin1Encoding = GetLatin1Encoding();

        // 修复1：正确计算输出长度
        private static int GetEncodedLength(int inputLength)
        {
            int bits = inputLength * 8;
            return (bits + 5) / 6; // 向上取整到最近的整数
        }

#if NET6_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Encoding GetLatin1Encoding() => Encoding.Latin1;
#else
        private static Encoding GetLatin1Encoding() => Encoding.GetEncoding(28591);
#endif

        public static string Encode(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            byte[]? rentedBuffer = null;
            try
            {
                var byteCount = GetMaxByteCount(input, encoding);
                rentedBuffer = ArrayPool<byte>.Shared.Rent(byteCount);
                var bytes = rentedBuffer.AsSpan();
                var written = GetBytes(input, bytes, encoding);
                return Encode(bytes.Slice(0, written));
            }
            finally
            {
                if (rentedBuffer != null)
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }

        private static unsafe int GetBytes(string input, Span<byte> destination, StringEncoding encoding)
        {
            fixed (char* pInput = input)
            fixed (byte* pDest = destination)
            {
                return encoding switch
                {
                    StringEncoding.UTF8 => Encoding.UTF8.GetBytes(pInput, input.Length, pDest, destination.Length),
                    StringEncoding.UTF16LE => Encoding.Unicode.GetBytes(pInput, input.Length, pDest, destination.Length),
                    StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetBytes(pInput, input.Length, pDest, destination.Length),
                    StringEncoding.ASCII => Encoding.ASCII.GetBytes(pInput, input.Length, pDest, destination.Length),
                    StringEncoding.UTF32 => Encoding.UTF32.GetBytes(pInput, input.Length, pDest, destination.Length),
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => s_latin1Encoding.GetBytes(pInput, input.Length, pDest, destination.Length),
#endif
                    _ => throw new NotSupportedException("Unsupported encoding")
                };
            }
        }

        public static unsafe string Encode(ReadOnlySpan<byte> input)
        {
            if (input.IsEmpty) return string.Empty;

            int outputLength = GetEncodedLength(input.Length);
            char[]? rentedArray = null;

            try
            {
                rentedArray = ArrayPool<char>.Shared.Rent(outputLength);
                Span<char> output = rentedArray;

                fixed (byte* pInput = input)
                fixed (char* pOutput = output)
                {
                    byte* currentInput = pInput;
                    char* currentOutput = pOutput;
                    int remaining = input.Length;
                    int outputIndex = 0;

                    // 修复2：改进的位处理逻辑
                    ulong buffer = 0;
                    int bitsInBuffer = 0;

                    while (remaining > 0 || bitsInBuffer > 0)
                    {
                        while (bitsInBuffer < 24 && remaining > 0)
                        {
                            buffer = (buffer << 8) | *currentInput++;
                            bitsInBuffer += 8;
                            remaining--;
                        }

                        int take = Math.Min(6, bitsInBuffer);
                        if (take == 0) break;

                        int index = (int)(((uint)(buffer >> (bitsInBuffer - take))) & ((1 << take) - 1));
                        index <<= (6 - take);
                        *currentOutput++ = Characters[index];
                        outputIndex++;

                        bitsInBuffer -= take;
                    }

                    return new string(pOutput, 0, outputIndex);
                }
            }
            finally
            {
                if (rentedArray != null)
                    ArrayPool<char>.Shared.Return(rentedArray);
            }
        }

        public static string Decode(string base62, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (string.IsNullOrEmpty(base62)) return string.Empty;

            byte[]? rentedBuffer = null;
            try
            {
                var maxByteCount = GetMaxByteCount(base62);
                rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
                var bytes = rentedBuffer.AsSpan();
                var written = DecodeInternal(base62, bytes);
                return GetString(bytes.Slice(0, written), encoding);
            }
            finally
            {
                if (rentedBuffer != null)
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }

        private static unsafe string GetString(ReadOnlySpan<byte> bytes, StringEncoding encoding)
        {
            fixed (byte* pBytes = bytes)
            {
                return encoding switch
                {
                    StringEncoding.UTF8 => Encoding.UTF8.GetString(pBytes, bytes.Length),
                    StringEncoding.UTF16LE => Encoding.Unicode.GetString(pBytes, bytes.Length),
                    StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetString(pBytes, bytes.Length),
                    StringEncoding.ASCII => Encoding.ASCII.GetString(pBytes, bytes.Length),
                    StringEncoding.UTF32 => Encoding.UTF32.GetString(pBytes, bytes.Length),
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => s_latin1Encoding.GetString(pBytes, bytes.Length),
#endif
                    _ => throw new NotSupportedException("Unsupported encoding")
                };
            }
        }

        public static unsafe int DecodeInternal(string base62, Span<byte> output)
        {
            fixed (char* pInput = base62)
            fixed (byte* pOutput = output)
            {
                char* currentChar = pInput;
                byte* currentByte = pOutput;
                int outputIndex = 0;
                ulong buffer = 0;
                int bits = 0;

                for (int i = 0; i < base62.Length; i++)
                {
                    char c = *currentChar++;
                    int value = Characters.IndexOf(c);
                    if (value < 0) throw new ArgumentException("Invalid Base62 character: " + c);

                    // 修复3：正确的位操作
                    buffer = (buffer << 6) | (ulong)value;
                    bits += 6;

                    while (bits >= 8)
                    {
                        bits -= 8;
                        *currentByte++ = (byte)(buffer >> bits);
                        outputIndex++;
                        buffer &= (1UL << bits) - 1;
                    }
                }

                // 处理剩余位（如果需要）
                if (bits > 0)
                {
                    *currentByte++ = (byte)(buffer << (8 - bits));
                    outputIndex++;
                }

                return outputIndex;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetMaxByteCount(int charCount, StringEncoding encoding) => encoding switch
        {
            StringEncoding.UTF8 => Encoding.UTF8.GetMaxByteCount(charCount),
            StringEncoding.UTF16LE => Encoding.Unicode.GetMaxByteCount(charCount),
            StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetMaxByteCount(charCount),
            StringEncoding.ASCII => Encoding.ASCII.GetMaxByteCount(charCount),
            StringEncoding.UTF32 => Encoding.UTF32.GetMaxByteCount(charCount),
#if NET6_0_OR_GREATER
        StringEncoding.Latin1 => charCount,
#endif
            _ => throw new NotSupportedException("Unsupported encoding")
        };

        private static int GetMaxByteCount(string input, StringEncoding encoding)
            => GetMaxByteCount(input.Length, encoding);

        private static int GetMaxByteCount(string base62)
            => (int)Math.Floor(base62.Length * 6 / 8.0);

        public override string ToString() => Characters;
    }
}
