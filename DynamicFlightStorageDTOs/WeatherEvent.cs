using MessagePack;

namespace DynamicFlightStorageDTOs
{
    [MessagePackObject]
    public class WeatherEvent
    {
        [Key(0)]
        public Weather? Weather { get; set; }
        [Key(1)]
        public required DateTime TimeStamp { get; set; }
        [Key(2)]
        public Dictionary<string, List<Weather>>? FullWeatherServiceData { get; set; }
    }
}
