using DynamicFlightStorageDTOs;

namespace BasicEventDataStore
{
    public class BasicEventDataStore : IEventDataStore
    {
        private readonly HashSet<FlightWrapper> _flights = new HashSet<FlightWrapper>();
        private readonly IWeatherService _weatherService;
        private readonly IRecalculateFlightEventPublisher _flightRecalculation;
        public BasicEventDataStore(IWeatherService weatherService, IRecalculateFlightEventPublisher recalculateFlightEventPublisher)
        {
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
            _flightRecalculation = recalculateFlightEventPublisher ?? throw new ArgumentNullException(nameof(recalculateFlightEventPublisher));
        }

        public Task AddOrUpdateFlightAsync(Flight flight)
        {
            if (_flights.FirstOrDefault(x => x.Flight.FlightIdentification == flight.FlightIdentification) is { } foundFlight)
            {
                // Update the flight in the datastore
                foundFlight.LastSeenCategories = GetWeatherCategoriesForFlight(flight);
                foundFlight.ToBeRecalculated = false;
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

        public async Task AddWeatherAsync(Weather weather)
        {
            foreach (var flightWrapper in _flights)
            {
                var flight = flightWrapper.Flight;
                if (WeatherOverlapsFlight(flight, weather)
                    && IsAirportInFlight(flight, weather.Airport)
                    && !flightWrapper.ToBeRecalculated)
                {
                    // Check for airport last seen weather
                    if (flightWrapper.LastSeenCategories.TryGetValue(weather.Airport, out var lastSeenCat)
                        && lastSeenCat < weather.WeatherLevel)
                    {
                        // We need to recalculate
                        Console.WriteLine($"Recalculate flight: {flight.FlightIdentification}");
                        flightWrapper.ToBeRecalculated = true;
                        await _flightRecalculation.PublishRecalculationAsync(flight).ConfigureAwait(false);
                    }
                }
            }
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
                { flight.DepartureAirport, _weatherService.GetWeather(flight.DepartureAirport, flight.ScheduledTimeOfDeparture).WeatherLevel }
            };

            if (flight.DepartureAirport != flight.DestinationAirport)
            {
                weather.Add(flight.DestinationAirport, _weatherService.GetWeather(flight.DestinationAirport, flight.ScheduledTimeOfArrival).WeatherLevel);
            }

            var timeMiddleOfFlight = flight.ScheduledTimeOfDeparture + ((flight.ScheduledTimeOfArrival - flight.ScheduledTimeOfDeparture) / 2);
            foreach (var airport in flight.OtherRelatedAirports.Keys.Where(x => !weather.ContainsKey(x)).Distinct())
            {
                weather.Add(airport, _weatherService.GetWeather(airport, timeMiddleOfFlight).WeatherLevel);
            }
            return weather;
        }

        private static bool WeatherOverlapsFlight(Flight flight, Weather weather)
        {
            // Check if the first range ends before the second range starts or
            // if the second range ends before the first range starts
            if (flight.ScheduledTimeOfArrival < weather.ValidFrom
                || weather.ValidTo < flight.ScheduledTimeOfDeparture)
            {
                return false;
            }
            return true;
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

        public Task ResetAsync()
        {
            _flights.Clear();
            return Task.CompletedTask;
        }
    }
}
