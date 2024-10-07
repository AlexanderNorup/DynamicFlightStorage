using DynamicFlightStorageDTOs;

namespace DynamicFlightStorageSimulation.Events
{
    public class WeatherEvent
    {
        public WeatherEvent(Weather weather)
        {
            Weather = weather ?? throw new ArgumentNullException(nameof(weather));
        }

        public Weather Weather { get; }
    }
}
