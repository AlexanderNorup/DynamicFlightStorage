using DynamicFlightStorageDTOs;

namespace DynamicFlightStorageSimulation.Events
{
    public class FlightStorageEvent
    {
        public FlightStorageEvent(Flight flight)
        {
            Flight = flight ?? throw new ArgumentNullException(nameof(flight));
        }

        public Flight Flight { get; }
    }
}
