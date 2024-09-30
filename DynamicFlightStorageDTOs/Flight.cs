using System.Diagnostics;

namespace DynamicFlightStorageDTOs
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class Flight : IFlight
    {
        public required string FlightIdentification { get; set; }
        public required string DepartureAirport { get; set; }
        public required string DestinationAirport { get; set; }
        public Dictionary<string, string> OtherRelatedAirports { get; set; } = new();
        public List<RouteNode>? Route { get; set; }
        public DateTime ScheduledTimeOfDeparture { get; set; }
        public DateTime ScheduledTimeOfArrival { get; set; }

        private string GetDebuggerDisplay()
        {
            return $"{DepartureAirport} - {DestinationAirport} @ {ScheduledTimeOfDeparture}";
        }
    }
}