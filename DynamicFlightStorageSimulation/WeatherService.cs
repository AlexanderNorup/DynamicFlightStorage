using DynamicFlightStorageDTOs;

namespace DynamicFlightStorageSimulation
{
    public class WeatherService : IWeatherService
    {
        private readonly Dictionary<string, List<Weather>> _weather = new ();
        public Weather GetWeather(string airport, DateTime dateTime)
        {
            if (_weather.TryGetValue(airport, out var weatherInstances))
            {
                var weather = weatherInstances.Find(weather => weather.ValidFrom <= dateTime && weather.ValidTo >= dateTime);
        
                if (weather is not null)
                {
                    return weather;
                }
            }
            return new Weather
            {
                Airport = airport,
                ValidFrom = dateTime,
                ValidTo = dateTime,
                WeatherLevel = WeatherCategory.Undefined
            };
        }

        public void AddWeather(string airport, DateTime start, DateTime end, WeatherCategory weather)
        {
            if (!_weather.ContainsKey(airport))
            {
                _weather[airport] = new List<Weather>();
            }
            _weather[airport].Add(new Weather
            {
                WeatherLevel = weather,
                Airport = airport,
                ValidFrom = start,
                ValidTo = end
            });
        }
    }
}
