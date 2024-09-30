
namespace DynamicFlightStorageDTOs
{
    public interface IFlight
    {
        string FlightIdentification { get; set; }
        string DepartureAirport { get; set; }
        string DestinationAirport { get; set; }
        Dictionary<string, string> OtherRelatedAirports { get; set; }
        List<RouteNode>? Route { get; set; }
        DateTime ScheduledTimeOfDeparture { get; set; }
        DateTime ScheduledTimeOfArrival { get; set; }
    }
}