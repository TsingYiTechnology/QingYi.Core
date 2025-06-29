#if NET5_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Text;

namespace QingYi.Core.Codec.Base
{
    public class Base26
    {
        private readonly bool _useUpperCase;
        private readonly int _minLength;
        private readonly string _charSet;
        private readonly byte[] _charMap = new byte[128]; // ASCII映射表

        /// <summary>
        /// 初始化Base26编解码器
        /// </summary>
        /// <param name="useUpperCase">是否使用大写字母（默认true）</param>
        /// <param name="minLength">编码最小长度（不足时填充'='）</param>
        public Base26(bool useUpperCase = true, int minLength = 0)
        {
            _useUpperCase = useUpperCase;
            _minLength = minLength;
            _charSet = _useUpperCase ? "ABCDEFGHIJKLMNOPQRSTUVWXYZ" : "abcdefghijklmnopqrstuvwxyz";

            // 初始化字符映射表
            for (int i = 0; i < _charMap.Length; i++)
            {
                _charMap[i] = 0xFF; // 0xFF表示无效字符
            }

            for (byte idx = 0; idx < 26; idx++)
            {
                _charMap[_charSet[idx]] = idx;
            }
        }

        /// <summary>
        /// 返回当前字符集
        /// </summary>
        public override string ToString() => _charSet;

        #region 字节数组编解码
        /// <summary>
        /// 编码字节数组
        /// </summary>
        public string Encode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) return string.Empty;

            List<byte> digits = new List<byte>();
            unsafe
            {
                fixed (byte* ptr = data)
                {
                    byte[] buffer = new byte[data.Length];
                    fixed (byte* bufPtr = buffer)
                    {
                        Buffer.MemoryCopy(ptr, bufPtr, data.Length, data.Length);
                        byte* start = bufPtr;
                        int currentLen = data.Length;

                        while (currentLen > 0)
                        {
                            int remainder = 0;
                            bool allZero = true;

                            for (int i = 0; i < currentLen; i++)
                            {
                                int temp = (remainder << 8) | start[i];
                                start[i] = (byte)(temp / 26);
                                remainder = temp % 26;

                                if (start[i] != 0) allZero = false;
                            }

                            digits.Add((byte)remainder);

                            if (allZero) break;

                            while (currentLen > 0 && *start == 0)
                            {
                                start++;
                                currentLen--;
                            }
                        }
                    }
                }
            }

            digits.Reverse();
            return BuildString(digits);
        }

        /// <summary>
        /// 解码Base26字符串
        /// </summary>
        public byte[] Decode(string base26)
        {
            if (base26 == null) throw new ArgumentNullException(nameof(base26));
            if (base26.Length == 0) return Array.Empty<byte>();

            // 跳过前导填充字符'='
            int startIndex = 0;
            while (startIndex < base26.Length && base26[startIndex] == '=')
                startIndex++;

            if (startIndex == base26.Length)
                return Array.Empty<byte>();

            // 转换为数字序列
            List<byte> digits = new List<byte>(base26.Length - startIndex);
            for (int i = startIndex; i < base26.Length; i++)
            {
                char c = base26[i];
                // 遇到填充字符停止解码
                if (c == '=') break;

                if (c >= 128 || _charMap[c] == 0xFF)
                    throw new FormatException($"无效Base26字符: '{c}'");

                digits.Add(_charMap[c]);
            }

            // 大整数乘法转换
            List<byte> result = new List<byte>(digits.Count * 2);
            foreach (byte digit in digits)
            {
                int carry = digit;
                for (int i = 0; i < result.Count; i++)
                {
                    int temp = result[i] * 26 + carry;
                    result[i] = (byte)(temp & 0xFF);
                    carry = temp >> 8;
                }

                while (carry > 0)
                {
                    result.Add((byte)(carry & 0xFF));
                    carry >>= 8;
                }
            }

            result.Reverse();
            return result.ToArray();
        }
        #endregion

        #region 字符串编解码
        /// <summary>
        /// 编码字符串（使用UTF8编码）
        /// </summary>
        public string Encode(string text) => Encode(text, StringEncoding.UTF8);

        /// <summary>
        /// 编码字符串（指定编码）
        /// </summary>
        public string Encode(string text, StringEncoding encoding)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            return Encode(GetBytes(text, encoding));
        }

        /// <summary>
        /// 解码为字符串（使用UTF8编码）
        /// </summary>
        public string DecodeToString(string base26) => DecodeToString(base26, StringEncoding.UTF8);

        /// <summary>
        /// 解码为字符串（指定编码）
        /// </summary>
        public string DecodeToString(string base26, StringEncoding encoding)
        {
            if (base26 == null) throw new ArgumentNullException(nameof(base26));
            byte[] data = Decode(base26);
            return GetString(data, encoding);
        }
        #endregion

        #region 私有辅助方法
        private string BuildString(List<byte> digits)
        {
            StringBuilder sb = new StringBuilder(digits.Count);
            foreach (byte digit in digits)
            {
                sb.Append(_charSet[digit]);
            }

            // 填充前导'='
            if (sb.Length < _minLength)
            {
                sb.Insert(0, new string('=', _minLength - sb.Length));
            }

            return sb.ToString();
        }

        private static byte[] GetBytes(string text, StringEncoding encoding)
        {
            switch (encoding)
            {
                case StringEncoding.UTF8:
                    return Encoding.UTF8.GetBytes(text);
                case StringEncoding.UTF16LE:
                    return Encoding.Unicode.GetBytes(text);
                case StringEncoding.UTF16BE:
                    return Encoding.BigEndianUnicode.GetBytes(text);
                case StringEncoding.ASCII:
                    return Encoding.ASCII.GetBytes(text);
                case StringEncoding.UTF32:
                    return Encoding.UTF32.GetBytes(text);
#if NET6_0_OR_GREATER
                case StringEncoding.Latin1:
                    return Encoding.Latin1.GetBytes(text);
#endif
                case StringEncoding.UTF7:
#pragma warning disable SYSLIB0001, CS0618
                    return Encoding.UTF7.GetBytes(text);
#pragma warning restore SYSLIB0001, CS0618
                default:
                    throw new ArgumentOutOfRangeException(nameof(encoding), "不支持的编码格式");
            }
        }

        private static string GetString(byte[] data, StringEncoding encoding)
        {
            switch (encoding)
            {
                case StringEncoding.UTF8:
                    return Encoding.UTF8.GetString(data);
                case StringEncoding.UTF16LE:
                    return Encoding.Unicode.GetString(data);
                case StringEncoding.UTF16BE:
                    return Encoding.BigEndianUnicode.GetString(data);
                case StringEncoding.ASCII:
                    return Encoding.ASCII.GetString(data);
                case StringEncoding.UTF32:
                    return Encoding.UTF32.GetString(data);
#if NET6_0_OR_GREATER
                case StringEncoding.Latin1:
                    return Encoding.Latin1.GetString(data);
#endif
                case StringEncoding.UTF7:
#pragma warning disable SYSLIB0001, CS0618
                    return Encoding.UTF7.GetString(data);
#pragma warning restore SYSLIB0001, CS0618
                default:
                    throw new ArgumentOutOfRangeException(nameof(encoding), "不支持的编码格式");
            }
        }
        #endregion
    }
}
#endif
