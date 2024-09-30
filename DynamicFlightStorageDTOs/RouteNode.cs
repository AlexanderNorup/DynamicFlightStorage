using System.Diagnostics;

namespace DynamicFlightStorageDTOs
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class RouteNode
    {
        public required string PointIdentifier { get; set; }
        public string? PointType { get; set; }
        public double Lattitude { get; set; }
        public double Longitude { get; set; }
        public int FlightLevel { get; set; }
        public DateTime TimeOverWaypoint { get; set; }

        private string GetDebuggerDisplay()
        {
            return $"{PointIdentifier} @ FL{FlightLevel} ({PointType})";
        }
    }
}