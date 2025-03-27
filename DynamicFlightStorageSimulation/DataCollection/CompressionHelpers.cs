using SevenZip;

namespace DynamicFlightStorageSimulation.DataCollection
{
    // Stolen from https://gist.github.com/ststeiger/cb9750664952f775a341
    public static class CompressionHelpers
    {
        static readonly int dictionary = 1 << 23;

        // static Int32 posStateBits = 2;
        // static Int32 litContextBits = 3; // for normal files
        // UInt32 litContextBits = 0; // for 32-bit data
        // static Int32 litPosBits = 0;
        // UInt32 litPosBits = 2; // for 32-bit data
        // static Int32 algorithm = 2;
        // static Int32 numFastBytes = 128;

        static readonly bool eos = false;

        static readonly CoderPropID[] propIDs =
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

        // these are the default properties, keeping it simple for now:
        static readonly object[] properties =
                {
                    (System.Int32)(dictionary),
                    (System.Int32)(2),
                    (System.Int32)(3),
                    (System.Int32)(0),
                    (System.Int32)(2),
                    (System.Int32)(128),
                    "bt4",
                    eos
                };


        public static byte[] Compress(byte[] inputBytes)
        {
            byte[] retVal;
            SevenZip.Compression.LZMA.Encoder encoder = new();
            encoder.SetCoderProperties(propIDs, properties);

            using (MemoryStream strmInStream = new(inputBytes))
            {
                using (MemoryStream strmOutStream = new())
                {
                    encoder.WriteCoderProperties(strmOutStream);
                    long fileSize = strmInStream.Length;
                    for (int i = 0; i < 8; i++)
                        strmOutStream.WriteByte((byte)(fileSize >> (8 * i)));

                    encoder.Code(strmInStream, strmOutStream, -1, -1, null);
                    retVal = strmOutStream.ToArray();
                } // End Using outStream

            } // End Using inStream 

            return retVal;
        } // End Function Compress

        public static byte[] Decompress(byte[] inputBytes)
        {
            byte[] retVal;

            SevenZip.Compression.LZMA.Decoder decoder = new();

            using (MemoryStream strmInStream = new(inputBytes))
            {
                strmInStream.Seek(0, 0);

                using (MemoryStream strmOutStream = new())
                {
                    byte[] properties2 = new byte[5];
                    if (strmInStream.Read(properties2, 0, 5) != 5)
                        throw (new Exception("input .lzma is too short"));

                    long outSize = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        int v = strmInStream.ReadByte();
                        if (v < 0)
                            throw (new Exception("Can't Read 1"));
                        outSize |= ((long)(byte)v) << (8 * i);
                    } // Next i 

                    decoder.SetDecoderProperties(properties2);

                    long compressedSize = strmInStream.Length - strmInStream.Position;
                    decoder.Code(strmInStream, strmOutStream, compressedSize, outSize, null);

                    retVal = strmOutStream.ToArray();
                } // End Using newOutStream 

            } // End Using newInStream 

            return retVal;
        } // End Function Decompress 
    }
}