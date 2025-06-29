#if NET6_0_OR_GREATER
#pragma warning disable CA2014, CS0618, SYSLIB0001
#nullable enable
using System;
using System.Buffers;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace QingYi.Core.Codec.Base
{
    /// <summary>
    /// Base36 codec library.<br />
    /// Base36 编解码库。
    /// </summary>
    public unsafe class Base36
    {
        private const string Base36Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const int ByteSize = 256;
        private const int Base = 36;

        private static readonly uint[] _pow36 = { 1, 36, 1296, 46656, 1679616, 60466176, 2176782336 };

        private static readonly int[] _reverseBase36Map = new int[ByteSize];

        static Base36()
        {
            Array.Fill(_reverseBase36Map, -1);  // 初始化所有位置为-1
            for (var i = 0; i < Base36Chars.Length; i++)
            {
                // 大写字母
                _reverseBase36Map[Base36Chars[i]] = i;
                // 小写字母
                _reverseBase36Map[char.ToLowerInvariant(Base36Chars[i])] = i;
            }
        }

        /// <summary>
        /// Gets the base36-encoded character set.<br />
        /// 获取 Base36 编码的字符集。
        /// </summary>
        /// <returns>The base36-encoded character set.<br />Base36 编码的字符集</returns>
        public override string ToString() => Base36Chars;

        /// <summary>
        /// Base36 encoding of the string.<br />
        /// 将字符串进行Base36编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string Encode(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var bytes = GetBytes(input, encoding);
            return Encode(bytes);
        }

        /// <summary>
        /// Base36 decoding of the string.<br />
        /// 将字符串进行Base36解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string Decode(string input, StringEncoding encoding = StringEncoding.UTF8)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var bytes = DecodeToBytes(input);
            return GetString(bytes, encoding);
        }

        private static string Encode(byte[] input)
        {
            if (input == null || input.Length == 0) return string.Empty;

            int outputLength = CalculateEncodedLength(input);
            char[]? array = null;
            
            try
            {
                Span<char> buffer = outputLength <= 1024
                    ? stackalloc char[outputLength]
                    : (array = ArrayPool<char>.Shared.Rent(outputLength));

                fixed (byte* bytesPtr = input)
                fixed (char* resultPtr = &MemoryMarshal.GetReference(buffer))
                {
                    int actualLength = InternalEncode(bytesPtr, input.Length, resultPtr);
                    return new string(resultPtr, 0, actualLength);
                }
            }
            finally
            {
                if (array != null)
                    ArrayPool<char>.Shared.Return(array);
            }
        }

        private static byte[] DecodeToBytes(string input)
        {
            if (string.IsNullOrEmpty(input)) return Array.Empty<byte>();

            fixed (char* charsPtr = input)
            {
                return InternalDecode(charsPtr, input.Length);
            }
        }

        // 完整IL实现的反转方法
        private static DynamicMethod CreateReverseBytesMethod()
        {
            var dm = new DynamicMethod(
                "ReverseBytes",
                typeof(void),
                new[] { typeof(byte*), typeof(int) },
                typeof(Base36).Module,
                skipVisibility: true);

            var il = dm.GetILGenerator();

            il.DeclareLocal(typeof(int)); // i
            il.DeclareLocal(typeof(byte)); // temp

            Label loop = il.DefineLabel();
            Label check = il.DefineLabel();

            // for (int i = 0; i < length / 2; i++)
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Stloc_0);
            il.Emit(OpCodes.Br_S, check);

            il.MarkLabel(loop);
            // byte temp = bytes[i];
            il.Emit(OpCodes.Ldarg_0);    // 加载bytes指针
            il.Emit(OpCodes.Ldloc_0);    // 加载i
            il.Emit(OpCodes.Add);        // 计算地址 bytes + i
            il.Emit(OpCodes.Ldind_U1);   // 读取字节值
            il.Emit(OpCodes.Stloc_1);    // 存入temp

            // bytes[i] = bytes[length - i - 1];
            il.Emit(OpCodes.Ldarg_0);    // bytes指针
            il.Emit(OpCodes.Ldloc_0);    // i
            il.Emit(OpCodes.Add);        // bytes + i

            il.Emit(OpCodes.Ldarg_0);    // bytes指针
            il.Emit(OpCodes.Ldarg_1);    // length
            il.Emit(OpCodes.Ldloc_0);    // i
            il.Emit(OpCodes.Sub);        // length - i
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Sub);        // length - i - 1
            il.Emit(OpCodes.Add);        // bytes + (length - i - 1)
            il.Emit(OpCodes.Ldind_U1);   // 读取字节值
            il.Emit(OpCodes.Stind_I1);   // 存入bytes[i]

            // bytes[length - i - 1] = temp;
            il.Emit(OpCodes.Ldarg_0);    // bytes指针
            il.Emit(OpCodes.Ldarg_1);    // length
            il.Emit(OpCodes.Ldloc_0);    // i
            il.Emit(OpCodes.Sub);        // length - i
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Sub);        // length - i - 1
            il.Emit(OpCodes.Add);        // bytes + (length - i - 1)

            il.Emit(OpCodes.Ldloc_1);    // 加载temp
            il.Emit(OpCodes.Stind_I1);   // 存储值

            // i++
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc_0);

            il.MarkLabel(check);
            // i < length / 2
            il.Emit(OpCodes.Ldloc_0);    // i
            il.Emit(OpCodes.Ldarg_1);    // length
            il.Emit(OpCodes.Ldc_I4_2);
            il.Emit(OpCodes.Div);        // length / 2
            il.Emit(OpCodes.Blt_S, loop);

            il.Emit(OpCodes.Ret);

            return dm;
        }

        // 定义专用委托类型
        private delegate void ReverseBytesDelegate(byte* bytes, int length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReverseBytes(byte* bytes, int length)
        {
            for (int i = 0; i < length / 2; i++)
            {
                (bytes[length - i - 1], bytes[i]) = (bytes[i], bytes[length - i - 1]);
            }
        }

        private static int InternalEncode(byte* bytesPtr, int length, char* result)
        {
            Span<byte> buffer = stackalloc byte[length];
            new ReadOnlySpan<byte>(bytesPtr, length).CopyTo(buffer);

            int index = 0;

            while (!buffer.IsEmpty)
            {
                int remainder = 0;
                for (int i = 0; i < buffer.Length; i++)
                {
                    int temp = (remainder << 8) | buffer[i];
                    buffer[i] = (byte)(temp / Base);
                    remainder = temp % Base;
                }

                result[index++] = Base36Chars[remainder];
                
                // Trim leading zeros
                int trim = 0;
                while (trim < buffer.Length && buffer[trim] == 0)
                    trim++;
                
                buffer = buffer[trim..];
            }

            // 添加验证
            for (int i = 0; i < index; i++)
            {
                if (!IsValidBase36Char(result[i]))
                    throw new InvalidOperationException("Generated invalid character");
            }

            Reverse(result, index);

            return index;
        }

        private static bool IsValidBase36Char(char c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

        private static byte[] InternalDecode(char* charsPtr, int length)
        {
            Span<byte> result = stackalloc byte[64]; // 初始缓冲区大小 64
            int resultIndex = 0;

            for (int i = 0; i < length; i++)
            {
                char c = charsPtr[i];
                if (c >= _reverseBase36Map.Length || (c = char.ToUpperInvariant(c)) >= _reverseBase36Map.Length)
                {
                    throw new ArgumentException($"Invalid Base36 character: '{c}'");
                }

                int value = _reverseBase36Map[c];
                if (value < 0) throw new ArgumentException($"Invalid Base36 character: '{c}'");

                int carry = value;
                for (int j = 0; j < resultIndex; j++)
                {
                    carry += result[j] * Base;
                    result[j] = (byte)(carry & 0xFF);
                    carry >>= 8;
                }

                while (carry > 0)
                {
                    if (resultIndex >= result.Length)
                    {
                        Span<byte> newBuffer = stackalloc byte[result.Length * 2];
                        result.CopyTo(newBuffer);
                        result = newBuffer;
                    }

                    if (resultIndex >= result.Length)
                        throw new InvalidOperationException("Buffer growth failed");

                    result[resultIndex++] = (byte)(carry & 0xFF);
                    carry >>= 8;
                }
            }

            // 处理空输入情况
            if (resultIndex == 0)
                return Array.Empty<byte>();

            // 反转字节数组
            ReverseBytes((byte*)Unsafe.AsPointer(ref result.GetPinnableReference()), resultIndex);

            // 修剪前导零并复制到数组
            int firstNonZero = 0;
            while (firstNonZero < resultIndex && result[firstNonZero] == 0)
                firstNonZero++;

            int finalLength = resultIndex - firstNonZero;
            if (finalLength == 0)
                return Array.Empty<byte>();

            byte[] output = new byte[finalLength];
            result.Slice(firstNonZero, finalLength).CopyTo(output);
            return output;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Span<byte> GrowBuffer(ref Span<byte> buffer)
        {
            var newBuffer = new Span<byte>(new byte[buffer.Length * 2]);
            buffer.CopyTo(newBuffer);
            buffer = newBuffer;
            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Encoding GetEncoding(StringEncoding encoding)
        {
            return encoding switch
            {
                StringEncoding.UTF8 => Encoding.UTF8,
                StringEncoding.UTF16LE => Encoding.Unicode,
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode,
                StringEncoding.UTF32 => Encoding.UTF32,
                StringEncoding.UTF7 => Encoding.UTF7,
                StringEncoding.ASCII => Encoding.ASCII,
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1,
#endif
                _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
            };
        }

        private static byte[] GetBytes(string input, StringEncoding encoding)
        {
            var enc = GetEncoding(encoding);
            return enc.GetBytes(input);
        }

        private static string GetString(byte[] bytes, StringEncoding encoding)
        {
            var enc = GetEncoding(encoding);
            return enc.GetString(bytes);
        }

        private static int CalculateEncodedLength(byte[] input)
        {
            int bits = input.Length * 8;
            return (int)Math.Ceiling(bits / Math.Log2(Base)) + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Reverse(char* arr, int length)
        {
            for (int i = 0; i < length / 2; i++)
            {
                (arr[length - i - 1], arr[i]) = (arr[i], arr[length - i - 1]);
            }
        }
    }

    /// <summary>
    /// Static string extension of Base36 codec library.<br />
    /// Base36 编解码库的静态字符串拓展。
    /// </summary>
    public static class Base36Extension
    {
        /// <summary>
        /// Base36 encoding of the string.<br />
        /// 将字符串进行Base36编码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The encoded string.<br />被编码的字符串</returns>
        public static string EncodeBase36(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base36.Encode(input, encoding);

        /// <summary>
        /// Base36 decoding of the string.<br />
        /// 将字符串进行Base36解码。
        /// </summary>
        /// <param name="input">The string to be converted.<br />需要转换的字符串</param>
        /// <param name="encoding">The encoding of the string.<br />字符串的编码方式</param>
        /// <returns>The decoded string.<br />被解码的字符串</returns>
        public static string DecodeBase36(this string input, StringEncoding encoding = StringEncoding.UTF8) => Base36.Decode(input, encoding);
    }
}
#endif
