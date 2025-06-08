using QingYi.Core.Crypto;
using System.Text;

namespace CryptoTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string input = "11ww22ddwowowowo我我我我";
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] desKey = Encoding.UTF8.GetBytes("12345678"); // 8 bytes for DES key
            byte[] desIv = Encoding.UTF8.GetBytes("12345678"); // 8 bytes for DES IV

            if (Encoding.UTF8.GetString(DESCryptoHelper.Decrypt(DESCryptoHelper.Encrypt(bytes, desKey, desIv), desKey, desIv)) == input)
            {
                Console.WriteLine("DES encryption and decryption successful!");
            }
            else
            {
                Console.WriteLine($"DES encryption and decryption failed! Out: {Encoding.UTF8.GetString(DESCryptoHelper.Decrypt(DESCryptoHelper.Encrypt(bytes, desKey, desIv), desKey, desIv))}");
            }
        }
    }
}
