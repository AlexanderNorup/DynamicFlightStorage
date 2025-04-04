﻿using DynamicFlightStorageDTOs;

namespace BasicEventDataStore
{
    public class BasicEventDataStore : IEventDataStore
    {
        private readonly Dictionary<String, FlightWrapper> _flights = new Dictionary<String, FlightWrapper>();
        private readonly IWeatherService _weatherService;
        private readonly IRecalculateFlightEventPublisher _flightRecalculation;
        public BasicEventDataStore(IWeatherService weatherService, IRecalculateFlightEventPublisher recalculateFlightEventPublisher)
        {
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
            _flightRecalculation = recalculateFlightEventPublisher ?? throw new ArgumentNullException(nameof(recalculateFlightEventPublisher));
        }

        public Task AddOrUpdateFlightAsync(Flight flight)
        {
            //if (_flights.FirstOrDefault(x => x.Flight.FlightIdentification == flight.FlightIdentification) is { } foundFlight)
            if (_flights.GetValueOrDefault(flight.FlightIdentification) is { } foundFlight)
            {
                // Update the flight in the datastore
                foundFlight.LastSeenCategories = _weatherService.GetWeatherCategoriesForFlight(flight);
                foundFlight.ToBeRecalculated = false;
            }
            else
            {
                // Add the flight 
                _flights.Add(flight.FlightIdentification,
                    new FlightWrapper()
                    {
                        Flight = flight,
                        LastSeenCategories = _weatherService.GetWeatherCategoriesForFlight(flight)
                    });
            }

            return Task.CompletedTask;
        }

        public async Task AddWeatherAsync(Weather weather, DateTime recievedTime)
        {
            foreach (var flightWrapper in _flights.Values)
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
                        await _flightRecalculation.PublishRecalculationAsync(flight.FlightIdentification, weather.Id, DateTime.UtcNow - recievedTime).ConfigureAwait(false);
                    }
                }
            }
        }

        public Task DeleteFlightAsync(string id)
        {
            _flights.Remove(id);
            //_flights.RemoveWhere(f => f.Flight.FlightIdentification == id);
            return Task.CompletedTask;
        }

        private static bool WeatherOverlapsFlight(Flight flight, Weather weather)
        {
            // Check if the first range ends before the second range starts or
            // if the second range ends before the first range starts

            return flight.ScheduledTimeOfDeparture <= weather.ValidTo
                && weather.ValidFrom <= flight.ScheduledTimeOfArrival;
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

        public Task StartAsync() => Task.CompletedTask;
    }
}
