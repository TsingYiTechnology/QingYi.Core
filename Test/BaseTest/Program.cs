using QingYi.Core.String.Base;

namespace BaseTest
{
    internal class Program
    {
        static void Main()
        {
            string testText = "Hello World!";

            #region Start
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.DarkYellow;

            // 输出文本
            Console.WriteLine("Base encode/decode test");
            #endregion

            #region Base2
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base2 ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base2.Encode(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base2.Decode(Base2.Encode(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base8
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base8 ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base8.EncodeString(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base8.DecodeString(Base8.EncodeString(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base10
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base10 ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base10.Encode(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base10.Decode(Base10.Encode(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base16
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base16 ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base16.Encode(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base16.Decode(Base16.Encode(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base32 RFC4648
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base32 RFC4648 ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base32RFC4648.Encode(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base32RFC4648.Decode(Base32RFC4648.Encode(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base32 Extended Hex
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base32 Extended Hex ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base32ExtendedHex.Encode(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base32ExtendedHex.Decode(Base32ExtendedHex.Encode(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base32 z-base-32
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base32 z-base-32 ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base32z.Encode(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base32z.Decode(Base32z.Encode(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base32 Crockford's
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base32 Crockford's ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base32Crockford.Encode(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base32Crockford.Decode(Base32Crockford.Encode(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base32 GeoHash
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base32 GeoHash ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base32GeoHash.Encode(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base32GeoHash.Decode(Base32GeoHash.Encode(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base32 Word-safe alphabet
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base32 Word-safe alphabet ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base32WordSafe.Encode(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base32WordSafe.Decode(Base32WordSafe.Encode(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base36
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base36 ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base36.Encode(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base36.Decode(Base36.Encode(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base45
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base45 ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base45.Encode(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base45.Decode(Base45.Encode(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base56
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base56 ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base56.EncodeString(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base56.DecodeString(Base56.EncodeString(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base58
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base58 ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base58.Encode(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base58.Decode(Base58.Encode(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base62
            #endregion

            #region Base64
            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.Write("·Base64 ");

            // 设置前景颜色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Encode: ");
            Console.Write($"{Base64.Encode(testText, StringEncoding.UTF8)}  ");
            Console.Write("Decode: ");
            Console.Write($"{Base64.Decode(Base64.Encode(testText, StringEncoding.UTF8), StringEncoding.UTF8)}\n");

            // 恢复为默认颜色
            Console.ResetColor();

            GC.Collect();
            #endregion

            #region Base85
            #endregion

            #region Base91
            #endregion

            #region Base92
            #endregion

            #region Base94
            #endregion

            #region Base100
            #endregion

            #region Base122
            #endregion

            #region Base128
            #endregion

            #region BaseXml
            #endregion

            GC.Collect();

            Console.ReadLine();
        }
    }
}
