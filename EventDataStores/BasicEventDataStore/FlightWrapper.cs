using DynamicFlightStorageDTOs;

namespace BasicEventDataStore
{
    public class FlightWrapper
    {
        public required Flight Flight { get; set; }
        public required Dictionary<string, WeatherCategory> LastSeenCategories { get; set; }
        // Could also be named "IsRecalculating" depending on how you look at it
        public bool ToBeRecalculated { get; set; } = false;

        public override bool Equals(object? obj)
        {
            return obj is FlightWrapper wrapper &&
                   EqualityComparer<Flight>.Default.Equals(Flight, wrapper.Flight) &&
                   LastSeenCategories == wrapper.LastSeenCategories;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Flight, LastSeenCategories);
        }
    }
}
