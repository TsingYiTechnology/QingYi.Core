#if NET5_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Text;

namespace QingYi.Core.Codec.Base
{
    public class Base91
    {
        /// <summary>
        /// Base91字符表（标准ASCII 33-126排除引号和反斜杠）
        /// </summary>
        private const string EncodeTable = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#$%&()*+,./:;<=>?@[]^_`{|}~";

        /// <summary>
        /// 解码映射表（256元素快速查找）
        /// </summary>
        private static readonly sbyte[] DecodeTable = new sbyte[256];

        static Base91()
        {
            // 初始化解码表（无效字符为-1）
            Array.Fill(DecodeTable, (sbyte)-1);
            for (sbyte i = 0; i < EncodeTable.Length; i++)
            {
                char c = EncodeTable[i];
                DecodeTable[c] = i;
            }
        }

        #region 编码接口
        public static string Encode(byte[] data) => EncodeCore(data);

        public static string Encode(string text, StringEncoding encoding = StringEncoding.UTF8)
        {
            byte[] data = GetBytes(text, encoding);
            return EncodeCore(data);
        }
        #endregion

        #region 解码接口
        public static byte[] Decode(string base91Text) => DecodeCore(base91Text);

        public static string DecodeToString(string base91Text, StringEncoding encoding = StringEncoding.UTF8)
        {
            byte[] data = DecodeCore(base91Text);
            return GetString(data, encoding);
        }
        #endregion

        #region 核心编解码逻辑
        private static unsafe string EncodeCore(byte[] input)
        {
            if (input == null || input.Length == 0) return string.Empty;

            // 预分配输出缓冲区（最大长度 = 原始长度 * 1.25）
            int outputLen = (int)Math.Ceiling(input.Length * 1.25) + 16;
            char[] outputBuffer = new char[outputLen];
            int outIndex = 0;
            int b = 0, n = 0;

            fixed (byte* inputPtr = input)
            fixed (char* outputPtr = outputBuffer)
            {
                byte* p = inputPtr;
                byte* end = p + input.Length;

                while (p < end)
                {
                    b |= *p << n;
                    n += 8;

                    if (n >= 13) // 有足够数据生成两个字符
                    {
                        int v = b & 0x1FFF; // 取13位
                        int shift = (v < 0x5F) ? 14 : 13; // 阈值优化
                        b >>= shift;
                        n -= shift;

                        // 写入两个字符
                        outputPtr[outIndex++] = EncodeTable[v % 91];
                        outputPtr[outIndex++] = EncodeTable[v / 91];
                    }
                    p++;
                }

                // 处理剩余数据
                if (n > 0)
                {
                    outputPtr[outIndex++] = EncodeTable[b % 91];
                    if (n > 7 || b > 90)
                        outputPtr[outIndex++] = EncodeTable[b / 91];
                }
            }
            return new string(outputBuffer, 0, outIndex);
        }

        private static unsafe byte[] DecodeCore(string input)
        {
            if (string.IsNullOrEmpty(input)) return Array.Empty<byte>();

            List<byte> output = new List<byte>(input.Length);
            int v = -1, b = 0, n = 0;

            fixed (char* inputPtr = input)
            {
                char* p = inputPtr;
                char* end = p + input.Length;

                while (p < end)
                {
                    // 跳过无效字符
                    if (*p < 0 || *p >= 256 || DecodeTable[*p] == -1)
                    {
                        p++;
                        continue;
                    }

                    int digit = DecodeTable[*p];
                    if (v < 0)
                    {
                        v = digit; // 第一个字符暂存
                    }
                    else
                    {
                        v += digit * 91;
                        int shift = (v & 0x1FFF) > 88 ? 13 : 14; // 动态位选择
                        b |= v << n;
                        n += shift;

                        // 提取完整字节
                        while (n >= 8)
                        {
                            output.Add((byte)(b & 0xFF));
                            b >>= 8;
                            n -= 8;
                        }
                        v = -1; // 重置状态
                    }
                    p++;
                }

                // 处理最后一个字符
                if (v > -1)
                {
                    b |= v << n;
                    n += 7; // 剩余数据补齐
                    while (n > 0)
                    {
                        output.Add((byte)(b & 0xFF));
                        b >>= 8;
                        n -= 8;
                    }
                }
            }
            return output.ToArray();
        }
        #endregion

        #region 编码转换辅助方法
        private static byte[] GetBytes(string text, StringEncoding encoding)
        {
            return encoding switch
            {
                StringEncoding.ASCII => Encoding.ASCII.GetBytes(text),
                StringEncoding.UTF16LE => Encoding.Unicode.GetBytes(text),
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetBytes(text),
                StringEncoding.UTF32 => Encoding.UTF32.GetBytes(text),
#pragma warning disable SYSLIB0001, CS0618
                StringEncoding.UTF7 => Encoding.UTF7.GetBytes(text),
#pragma warning restore SYSLIB0001, CS0618
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1.GetBytes(text),
#endif
                _ => Encoding.UTF8.GetBytes(text) // 默认UTF-8
            };
        }

        private static string GetString(byte[] data, StringEncoding encoding)
        {
            return encoding switch
            {
                StringEncoding.ASCII => Encoding.ASCII.GetString(data),
                StringEncoding.UTF16LE => Encoding.Unicode.GetString(data),
                StringEncoding.UTF16BE => Encoding.BigEndianUnicode.GetString(data),
                StringEncoding.UTF32 => Encoding.UTF32.GetString(data),
#pragma warning disable SYSLIB0001, CS0618
                StringEncoding.UTF7 => Encoding.UTF7.GetString(data),
#pragma warning restore SYSLIB0001, CS0618
#if NET6_0_OR_GREATER
                StringEncoding.Latin1 => Encoding.Latin1.GetString(data),
#endif
                _ => Encoding.UTF8.GetString(data) // 默认UTF-8
            };
        }
        #endregion
    }
}
#endif
