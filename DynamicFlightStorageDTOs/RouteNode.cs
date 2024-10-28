using MessagePack;
using System.Diagnostics;

namespace DynamicFlightStorageDTOs
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    [MessagePackObject]
    public class RouteNode
    {
        [Key(0)]
        public required string PointIdentifier { get; set; }
        [Key(1)]
        public string? PointType { get; set; }
        [Key(2)]
        public double Lattitude { get; set; }
        [Key(3)]
        public double Longitude { get; set; }
        [Key(4)]
        public int FlightLevel { get; set; }
        [Key(5)]
        public DateTime TimeOverWaypoint { get; set; }

        private string GetDebuggerDisplay()
        {
            return $"{PointIdentifier} @ FL{FlightLevel} ({PointType})";
        }
    }
}