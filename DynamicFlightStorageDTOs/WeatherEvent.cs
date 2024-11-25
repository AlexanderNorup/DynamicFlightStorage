using MessagePack;

namespace DynamicFlightStorageDTOs
{
    [MessagePackObject]
    public class WeatherEvent
    {
        [Key(0)]
        public required Weather Weather { get; set; }
        [Key(1)]
        public required DateTime TimeStamp { get; set; }
    }
}
