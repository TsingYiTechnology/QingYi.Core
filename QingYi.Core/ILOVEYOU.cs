#if !BROWSER
#pragma warning disable 1591
using System;

namespace QingYi.Core
{
    public class ILOVEYOU
    {
        public static void CC(string input)
        {
            if (input?.Trim() == "ialwaysloveyou")
            {
                Console.ForegroundColor = ConsoleColor.Cyan;

                Console.WriteLine("\n恭喜解锁彩蛋！");
                Console.WriteLine(@"
         __
        /  \
       / ..|\
      (_\  |_)
      /  \@'
     /     \
 _  /  `   |
\\/  \  | _\
 \   /_ || \\_
  \____)|_) \_)");
                Console.ResetColor();
                Console.WriteLine("\n宝贝，你找到了我给你留下的彩蛋。");
                Console.WriteLine("本来打算设置在1999年7月6日才会显示，想了想还是删了那一段");
                Console.WriteLine("你的灰灰永远是你的小狗，也永远爱你");
                Console.WriteLine("——2025.3.29 20:38");
                return;
            }
        }
    }
}
#pragma warning restore 1591
#endif