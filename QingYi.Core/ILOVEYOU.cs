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
                Console.WriteLine("——2025.03.29 20:38");
                Console.WriteLine("真的好想你啊，这个彩蛋你或许永远永远不会发现，但是它会永远留下，互联网只要还存续，它便会永存。");
                Console.WriteLine("——2025.05.04 02:49");
                return;
            }
        }
    }
}
#pragma warning restore 1591
#endif