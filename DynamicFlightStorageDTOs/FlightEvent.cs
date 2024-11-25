using MessagePack;

namespace DynamicFlightStorageDTOs
{
    [MessagePackObject]
    public class FlightEvent
    {
        [Key(0)]
        public required Flight Flight { get; set; }
        [Key(1)]
        public required DateTime TimeStamp { get; set; }
    }
}
