#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QingYi.Core.Compression.Unsafe.LZMA
{
    public class LzmaDecompressor : IDisposable
    {
        private readonly LzmaDecoder _decoder;

        public LzmaDecompressor()
        {
            _decoder = new LzmaDecoder();
        }

        public void Decompress(Stream input, Stream output)
        {
            // 读取LZMA属性头 (5字节)
            var properties = new byte[5];
            if (input.Read(properties) != 5)
                throw new InvalidDataException("Invalid LZMA header");

            int lc = properties[0] % 9;
            int lp = (properties[0] / 9) % 5;
            int pb = properties[0] / 45;
            uint dictionarySize = BitConverter.ToUInt32(properties, 1);

            _decoder.SetProperties(lc, lp, pb, dictionarySize);
            _decoder.Code(input, output);
        }

        public void Dispose()
        {
            _decoder.Dispose();
        }
    }
}
#endif