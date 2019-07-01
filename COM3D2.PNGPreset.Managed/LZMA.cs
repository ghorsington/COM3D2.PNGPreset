using System;
using System.IO;
using SevenZip;
using SevenZip.Compression.LZMA;

namespace COM3D2.PNGPreset.Managed
{
    internal static class LZMA
    {
        private const int DICTIONARY = 1 << 23;
        private const bool EOS = false;

        private static readonly CoderPropID[] PropIDs =
        {
            CoderPropID.DictionarySize,
            CoderPropID.PosStateBits,
            CoderPropID.LitContextBits,
            CoderPropID.LitPosBits,
            CoderPropID.Algorithm,
            CoderPropID.NumFastBytes,
            CoderPropID.MatchFinder,
            CoderPropID.EndMarker
        };

        private static readonly object[] Properties =
        {
            DICTIONARY,
            2,
            3,
            0,
            2,
            128,
            "bt4",
            EOS
        };

        public static byte[] Compress(MemoryStream inStream)
        {
            var outStream = new MemoryStream();
            var encoder = new Encoder();
            encoder.SetCoderProperties(PropIDs, Properties);
            encoder.WriteCoderProperties(outStream);
            var fileSize = inStream.Length;
            for (var i = 0; i < 8; i++)
                outStream.WriteByte((byte) (fileSize >> (8 * i)));
            encoder.Code(inStream, outStream, -1, -1, null);
            return outStream.ToArray();
        }

        public static MemoryStream Decompress(Stream inStream, int lengthPadding = 0)
        {
            var decoder = new Decoder();
            var newOutStream = new MemoryStream();

            var properties2 = new byte[5];
            if (inStream.Read(properties2, 0, 5) != 5)
                throw new Exception("input .lzma is too short");
            long outSize = 0;
            for (var i = 0; i < 8; i++)
            {
                var v = inStream.ReadByte();
                if (v < 0)
                    throw new Exception("Can't Read 1");
                outSize |= (long) (byte) v << (8 * i);
            }

            decoder.SetDecoderProperties(properties2);

            var compressedSize = inStream.Length - inStream.Position - lengthPadding;
            decoder.Code(inStream, newOutStream, compressedSize, outSize, null);
            return newOutStream;
        }
    }
}