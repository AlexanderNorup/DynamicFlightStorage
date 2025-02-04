using MessagePack;
using System.Diagnostics;

namespace DynamicFlightStorageDTOs
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    [MessagePackObject]
    public class FlightRecalculation
    {
        [Key(0)]
        public required string FlightIdentification { get; set; }

        [Key(1)]
        public required DateTime RecalculatedTime { get; set; }

        [Key(2)]
        public required string ExperimentId { get; set; }

        public override string ToString()
        {
            return $"Recaculation for {{{FlightIdentification}}}: @ {RecalculatedTime} (Experiment: {ExperimentId})";
        }

        private string GetDebuggerDisplay()
        {
            return ToString();
        }
    }
}
