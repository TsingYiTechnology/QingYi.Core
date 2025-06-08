using Compression;
using QingYi.Core.Compression;
using System.Text;

string text = "hello";
byte[] textBytes = Encoding.UTF8.GetBytes(text);
byte[] cTemp;
byte[] dTemp;

#region Deflate
cTemp = Deflate.Compress(textBytes);
dTemp = Deflate.Decompress(cTemp);
if (Validator.Check(textBytes, dTemp))
{
    Console.WriteLine("Deflate 验证通过");
}
else
{
    Console.WriteLine($"Deflate 验证失败。textBytes: {BitConverter.ToString(textBytes)}; cTemp: {BitConverter.ToString(cTemp)}; dTemp: {BitConverter.ToString(dTemp)}");
}
#endregion

#region LZ77
Lz77Token[] lz77Tokens = LZ77.Encode(textBytes);
dTemp = LZ77.Decode(lz77Tokens);
if (Validator.Check(textBytes, dTemp))
{
    Console.WriteLine("LZ77 验证通过");
}
else
{
    Console.WriteLine($"LZ77 验证失败。textBytes: {BitConverter.ToString(textBytes)}; cTemp: {lz77Tokens}; dTemp: {BitConverter.ToString(dTemp)}");
}
#endregion
