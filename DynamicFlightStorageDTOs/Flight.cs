using MessagePack;
using System.Diagnostics;

namespace DynamicFlightStorageDTOs
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    [MessagePackObject]
    public class Flight : IFlight
    {
        [Key(0)]
        public required string FlightIdentification { get; set; }
        [Key(1)]
        public required string DepartureAirport { get; set; }
        [Key(2)]
        public required string DestinationAirport { get; set; }
        [Key(3)]
        public Dictionary<string, string> OtherRelatedAirports { get; set; } = new();
        [Key(4)]
        public List<RouteNode>? Route { get; set; }
        [Key(5)]
        public DateTime ScheduledTimeOfDeparture { get; set; }
        [Key(6)]
        public DateTime ScheduledTimeOfArrival { get; set; }
        [Key(7)]
        public DateTime DatePlanned { get; set; }

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

        public override string ToString()
        {
            return $"{DepartureAirport} - {DestinationAirport} @ {ScheduledTimeOfDeparture}";
        }

        private string GetDebuggerDisplay()
        {
            return ToString();
        }

        public IEnumerable<string> GetAllAirports()
        {
            yield return DepartureAirport;
            yield return DestinationAirport;
            foreach (var airport in OtherRelatedAirports.Keys)
            {
                yield return airport;
            }
        }
    }
}