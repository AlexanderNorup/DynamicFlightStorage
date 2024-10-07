using DynamicFlightStorageDTOs;

namespace DynamicFlightStorageSimulation.Events
{
    public class FlightRecalculationEvent
    {
        public FlightRecalculationEvent(Flight flight)
        {
            Flight = flight ?? throw new ArgumentNullException(nameof(flight));
        }

        public Flight Flight { get; }
    }
}
