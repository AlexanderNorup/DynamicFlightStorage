using DynamicFlightStorageSimulation.DataCollection;
using MessagePack;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator
{
    [MessagePackObject]
    public record LagData([property: Key(0)] DateTime Timestamp, [property: Key(1)] int WeatherLag, [property: Key(2)] int FlightLag);

    public static class LagDataHelper
    {
        public static bool TryGetLagDataFromCompressedBytes(this byte[] compressed, out List<LagData> lagData)
        {
            lagData = null!;
            try
            {
                lagData = compressed.GetLagDataFromCompressedBytes();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static List<LagData> GetLagDataFromCompressedBytes(this byte[] compressed)
            => MessagePackSerializer.Deserialize<List<LagData>>(CompressionHelpers.Decompress(compressed), ConsumerDataLogger.MessagePackOptions);

        public static byte[] ConvertToCompressedBytes(this List<LagData> lagData)
            => CompressionHelpers.Compress(MessagePackSerializer.Serialize(lagData, ConsumerDataLogger.MessagePackOptions));
    }
}
