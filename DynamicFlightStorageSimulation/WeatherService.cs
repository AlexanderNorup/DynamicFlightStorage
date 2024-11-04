using DynamicFlightStorageDTOs;

namespace DynamicFlightStorageSimulation
{
    public class WeatherService : IWeatherService
    {
        private readonly Dictionary<string, List<Weather>> _weather = new();
        public Weather GetWeather(string airport, DateTime dateTime)
        {
            var backupWeather = new Weather
            {
                Airport = airport,
                ValidFrom = dateTime,
                ValidTo = dateTime,
                WeatherLevel = WeatherCategory.Undefined
            };

            if (_weather.TryGetValue(airport, out var weatherInstances))
            {
                var weather = weatherInstances.Find(weather => weather.ValidFrom <= dateTime && weather.ValidTo >= dateTime);

                if (weather is not null)
                {
                    return weather;
                }

                var smallestDiff = TimeSpan.MaxValue;

                foreach (var weatherInstance in weatherInstances)
                {
                    var diff1 = weatherInstance.ValidTo - dateTime;
                    var diff2 = weatherInstance.ValidFrom - dateTime;
                    var difference = TimeSpan.Compare(diff1, diff2) <= 0 ? diff1 : diff2;

                    if (smallestDiff > difference)
                    {
                        smallestDiff = difference;
                        backupWeather = weatherInstance;
                    }
                }
            }
            return backupWeather;
        }

        public void AddWeather(Weather weather)
        {
            var airport = weather.Airport;
            var weatherLevel = weather.WeatherLevel;
            var start = weather.ValidFrom;
            var end = weather.ValidTo;
            if (!_weather.ContainsKey(airport))
            {
                _weather[airport] = new List<Weather>();
            }
            _weather[airport].Add(new Weather
            {
                WeatherLevel = weatherLevel,
                Airport = airport,
                ValidFrom = start,
                ValidTo = end
            });
        }

        public void Clear()
        {
            _weather.Clear();
        }
    }
}
