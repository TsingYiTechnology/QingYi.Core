using System;
using System.IO;
using System.Text;

namespace QingYi.Core.FileUtility.IO
{
    /// <summary>
    /// Provides static utility methods for simplified file reading operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class offers a simplified API for common file reading scenarios, 
    /// abstracting the underlying <see cref="FileReader"/> implementation.
    /// </para>
    /// <para>
    /// Key characteristics:
    /// <list type="bullet">
    /// <item><description>Automatic resource management (disposal)</description></item>
    /// <item><description>Whole-file reading only (no partial reads)</description></item>
    /// <item><description>Supports binary/textual return formats</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Important limitations:
    /// <list type="bullet">
    /// <item><description>Not suitable for files larger than 2GB (int.MaxValue limitation)</description></item>
    /// <item><description>Text decoding uses UTF-8 without BOM handling</description></item>
    /// <item><description>Entire file loaded into memory</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public class FileReaderStatic
    {
        /// <summary>
        /// Specifies the return format for file content
        /// </summary>
        public enum ReturnType
        {
            /// <summary>
            /// Return file content as raw bytes (byte[])
            /// </summary>
            Bytes,

            /// <summary>
            /// Return file content as UTF-8 decoded string
            /// </summary>
            String,
        }

        /// <summary>
        /// Reads entire file content in the specified format
        /// </summary>
        /// <param name="filePath">Path to the target file</param>
        /// <param name="type">
        /// Return format specification (default: <see cref="ReturnType.String"/>)
        /// </param>
        /// <returns>
        /// File content as:
        /// <list type="bullet">
        /// <item><description>byte[] when <paramref name="type"/> is <see cref="ReturnType.Bytes"/></description></item>
        /// <item><description>string when <paramref name="type"/> is <see cref="ReturnType.String"/></description></item>
        /// </list>
        /// </returns>
        /// <exception cref="FileNotFoundException">Specified file does not exist</exception>
        /// <exception cref="IOException">File access error</exception>
        /// <exception cref="ArgumentOutOfRangeException">File size exceeds int.MaxValue (2GB)</exception>
        /// <remarks>
        /// <para>
        /// Implementation details:
        /// <list type="number">
        /// <item><description>Validates file existence</description></item>
        /// <item><description>Reads entire file using <see cref="FileReader"/></description></item>
        /// <item><description>Converts data format per <paramref name="type"/> parameter</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Memory considerations:
        /// <list type="bullet">
        /// <item><description>Allocates full file size in memory</description></item>
        /// <item><description>For large files, consider chunked reading instead</description></item>
        /// </list>
        /// </para>
        /// <example>
        /// Basic usage:
        /// <code>
        /// var text = FileReaderStatic.Read("data.txt") as string;
        /// var bytes = FileReaderStatic.Read("image.png", ReturnType.Bytes) as byte[];
        /// </code>
        /// </example>
        /// </remarks>
        public static object Read(string filePath, ReturnType type = ReturnType.String)
        {
            long fileLength = new FileInfo(filePath).Length;

            using(var reader = new FileReader(filePath))
            {
                var data = reader.Read(offset: 0, count: (int)fileLength); // return Span<byte>

                switch (type)
                {
                    case ReturnType.Bytes:
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                        return data.ToArray();
#elif NETSTANDARD2_0
                        byte[] copy = new byte[data.Length];
                        Array.Copy(data, copy, data.Length);
                        return copy;
#endif
                    case ReturnType.String:
                        return Encoding.UTF8.GetString(data);
                    default:
                        return Encoding.UTF8.GetString(data);
                }
            }
        }
    }
}
