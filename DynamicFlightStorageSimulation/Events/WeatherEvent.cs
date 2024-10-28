using DynamicFlightStorageDTOs;
using MessagePack;

namespace DynamicFlightStorageSimulation.Events
{
    [MessagePackObject]
    public class WeatherEvent
    {
        public WeatherEvent(Weather weather)
        {
            Weather = weather ?? throw new ArgumentNullException(nameof(weather));
        }

        [Key(0)]
        public Weather Weather { get; }
    }
}
