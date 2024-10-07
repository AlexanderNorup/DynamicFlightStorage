using DynamicFlightStorageDTOs;

namespace BasicEventDataStore
{
    public class BasicEventDataStore : IEventDataStore
    {
        private readonly HashSet<FlightWrapper> _flights = new HashSet<FlightWrapper>();
        private readonly IWeatherService _weatherService;
        public BasicEventDataStore(IWeatherService weatherService)
        {
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
        }

        public Task AddOrUpdateFlightAsync(Flight flight)
        {
            if (_flights.FirstOrDefault(x => x.Flight.FlightIdentification == flight.FlightIdentification) is { } foundFlight)
            {
                // Update the flight in the datastore
                foundFlight.LastSeenCategories = GetWeatherCategoriesForFlight(flight);
            }
            else
            {
                // Add the flight 
                _flights.Add(new FlightWrapper()
                {
                    Flight = flight,
                    LastSeenCategories = GetWeatherCategoriesForFlight(flight)
                });
            }

            return Task.CompletedTask;
        }

        public Task AddWeatherAsync(Weather weather)
        {
            foreach (var flightWrapper in _flights)
            {
                var flight = flightWrapper.Flight;
                if (flight.ScheduledTimeOfDeparture >= weather.ValidFrom
                    && flight.ScheduledTimeOfArrival <= weather.ValidTo
                    && IsAirportInFlight(flight, weather.Airport))
                {
                    // Check for airport last seen weather
                    if (flightWrapper.LastSeenCategories.TryGetValue(weather.Airport, out var lastSeenCat)
                        && lastSeenCat < weather.WeatherLevel)
                    {
                        // We need to recalculate
                        Console.WriteLine($"Recalculate flight: {flight.FlightIdentification}");
                    }
                }
            }
            return Task.CompletedTask;
        }

        public Task DeleteFlightAsync(string id)
        {
            _flights.RemoveWhere(f => f.Flight.FlightIdentification == id);
            return Task.CompletedTask;
        }

        private Dictionary<string, WeatherCategory> GetWeatherCategoriesForFlight(Flight flight)
        {
            var weather = new Dictionary<string, WeatherCategory>()
            {
                { flight.DepartureAirport, _weatherService.GetWeather(flight.DepartureAirport, flight.ScheduledTimeOfDeparture).WeatherLevel },
                { flight.DestinationAirport, _weatherService.GetWeather(flight.DestinationAirport, flight.ScheduledTimeOfArrival).WeatherLevel }
            };
            var timeMiddleOfFlight = flight.ScheduledTimeOfDeparture + ((flight.ScheduledTimeOfArrival - flight.ScheduledTimeOfDeparture) / 2);
            foreach (var airport in flight.OtherRelatedAirports.Keys)
            {
                weather.Add(airport, _weatherService.GetWeather(airport, timeMiddleOfFlight).WeatherLevel);
            }
            return weather;
        }

        private static bool IsAirportInFlight(Flight flight, string airport)
        {
            if (flight.DestinationAirport == airport
                || flight.DepartureAirport == airport)
            {
                return true;
            }

            if (flight.OtherRelatedAirports.ContainsKey(airport))
            {
                return true;
            }

            return false;
        }
    }
}
