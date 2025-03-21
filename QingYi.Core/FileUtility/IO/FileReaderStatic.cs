using System.IO;
using System.Text;

namespace QingYi.Core.FileUtility.IO
{
    public class FileReaderStatic
    {
        public enum ReturnType
        {
            Bytes,
            String,
        }

        public static object Read(string filePath, ReturnType type = ReturnType.String)
        {
            long fileLength = new FileInfo(filePath).Length;

            using var reader = new FileReader(filePath);
            var data = reader.Read(offset: 0, count: (int)fileLength); // return Span<byte>

            switch (type)
            {
                case ReturnType.Bytes:
                    return data.ToArray();
                case ReturnType.String:
                    return Encoding.UTF8.GetString(data);
                default:
                    return Encoding.UTF8.GetString(data);
            }
        }
    }
}
