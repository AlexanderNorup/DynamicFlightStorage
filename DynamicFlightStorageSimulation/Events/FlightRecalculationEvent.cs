using DynamicFlightStorageDTOs;
using MessagePack;

namespace DynamicFlightStorageSimulation.Events
{
    [MessagePackObject]
    public class FlightRecalculationEvent
    {
        public FlightRecalculationEvent(Flight flight)
        {
            Flight = flight ?? throw new ArgumentNullException(nameof(flight));
        }

        [Key(0)]
        public Flight Flight { get; }
    }
}
