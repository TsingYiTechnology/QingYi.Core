#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;

namespace QingYi.Core.Compression.Unsafe.LZMA
{
    internal static class BitOperations
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(uint value)
        {
            if (value == 0) return 32;
            return System.Numerics.BitOperations.TrailingZeroCount(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Log2(uint value)
        {
            return System.Numerics.BitOperations.Log2(value);
        }
    }
}
#endif