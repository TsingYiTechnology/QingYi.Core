using System.Text;
using QingYi.Core.FileUtility.IO;

namespace IOTest
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string str = "this is a test string";
            byte[] bytes = Encoding.UTF8.GetBytes(str);

            await FileWriterStatic.WriteAsync("text.txt", bytes);

            Console.WriteLine(FileReaderStatic.Read("text.txt"));

            Console.ReadLine();
        }
    }
}
