using DynamicFlightStorageDTOs;

namespace DynamicFlightStorageSimulation
{
    public class WeatherService : IWeatherService
    {
        private Dictionary<string, List<Weather>> _weather = new();
        public Dictionary<string, List<Weather>> Weather
        {
            get
            {
                return _weather;
            }
            set
            {
                ArgumentNullException.ThrowIfNull(value, nameof(Weather));
                _weather = value;
            }
        }

        public Weather GetWeather(string airport, DateTime dateTime)
        {
            var backupWeather = new Weather
            {
                Id = Guid.NewGuid().ToString(),
                Airport = airport,
                ValidFrom = dateTime,
                ValidTo = dateTime,
                WeatherLevel = WeatherCategory.Undefined,
                DateIssued = dateTime,
            };

            if (_weather.TryGetValue(airport, out var weatherInstances))
            {
                var weather = weatherInstances
                    .Where(weather => weather.ValidFrom <= dateTime && weather.ValidTo >= dateTime);

                if (weather.Any())
                {
                    return weather.MaxBy(x => x.DateIssued)!;
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
            var dateIssued = weather.DateIssued;
            if (!_weather.ContainsKey(airport))
            {
                _weather[airport] = new List<Weather>();
            }
            _weather[airport].Add(new Weather
            {
                Id = weather.Id,
                WeatherLevel = weatherLevel,
                Airport = airport,
                ValidFrom = start,
                ValidTo = end,
                DateIssued = dateIssued,
            });
        }

        public void ResetWeatherService()
        {
            _weather.Clear();
        }
    }
}
