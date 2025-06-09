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
            byte[] desKey = Encoding.UTF8.GetBytes("12345678");
            byte[] desIv = Encoding.UTF8.GetBytes("12345678");
            byte[] aesKey = Encoding.UTF8.GetBytes("1234567890abcdef");
            byte[] aesIv = Encoding.UTF8.GetBytes("1234567890abcdef");
            byte[] tdesKey = Encoding.UTF8.GetBytes("1234s65t1rf45sj81ma45678");
            byte[] tdesIv = Encoding.UTF8.GetBytes("12345678");

            #region DES
            if (Encoding.UTF8.GetString(DESCryptoHelper.Decrypt(DESCryptoHelper.Encrypt(bytes, desKey, desIv), desKey, desIv)) == input)
            {
                Console.WriteLine("DES encryption and decryption successful!");
            }
            else
            {
                Console.WriteLine($"DES encryption and decryption failed! Out: {Encoding.UTF8.GetString(DESCryptoHelper.Decrypt(DESCryptoHelper.Encrypt(bytes, desKey, desIv), desKey, desIv))}");
            }
            #endregion

            #region AES
            if (Encoding.UTF8.GetString(AESCryptoHelper.Decrypt(AESCryptoHelper.Encrypt(bytes, aesKey, aesIv), aesKey, aesIv)) == input)
            {
                Console.WriteLine("AES encryption and decryption successful!");
            }
            else
            {
                Console.WriteLine($"AES encryption and decryption failed! Out: {Encoding.UTF8.GetString(AESCryptoHelper.Decrypt(AESCryptoHelper.Encrypt(bytes, aesKey, aesIv), aesKey, aesIv))}");
            }
            #endregion

            #region TripleDES
            if (Encoding.UTF8.GetString(TripleDESHelper.Decrypt(TripleDESHelper.Encrypt(bytes, tdesKey, tdesIv), tdesKey, tdesIv)) == input)
            {
                Console.WriteLine("Triple DES encryption and decryption successful!");
            }
            else
            {
                Console.WriteLine($"Triple DES encryption and decryption failed! Out: {Encoding.UTF8.GetString(TripleDESHelper.Decrypt(TripleDESHelper.Encrypt(bytes, tdesKey, tdesIv), tdesKey, tdesIv))}");
            }
            #endregion
        }
    }
}
