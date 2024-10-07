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

        public override bool Equals(object? obj)
        {
            return obj is Flight flight &&
                   FlightIdentification == flight.FlightIdentification &&
                   DepartureAirport == flight.DepartureAirport &&
                   DestinationAirport == flight.DestinationAirport &&
                   EqualityComparer<Dictionary<string, string>>.Default.Equals(OtherRelatedAirports, flight.OtherRelatedAirports) &&
                   EqualityComparer<List<RouteNode>?>.Default.Equals(Route, flight.Route) &&
                   ScheduledTimeOfDeparture == flight.ScheduledTimeOfDeparture &&
                   ScheduledTimeOfArrival == flight.ScheduledTimeOfArrival;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(FlightIdentification, DepartureAirport, DestinationAirport, OtherRelatedAirports, Route, ScheduledTimeOfDeparture, ScheduledTimeOfArrival);
        }

        private string GetDebuggerDisplay()
        {
            return $"{DepartureAirport} - {DestinationAirport} @ {ScheduledTimeOfDeparture}";
        }
    }
}