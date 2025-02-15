﻿using DynamicFlightStorageDTOs;
using Microsoft.EntityFrameworkCore;
using SimplePostgreSQLDataStore.DbEntities;
using Testcontainers.PostgreSql;

namespace SimplePostgreSQLDataStore
{
    public class SimplePostgreSQLDataStore : IEventDataStore, IDisposable
    {
        private SimplePostgreSQLDbContext? _dbContext;
        private PostgreSqlContainer? _container;
        private readonly IWeatherService _weatherService;
        private readonly IRecalculateFlightEventPublisher _flightRecalculation;
        public SimplePostgreSQLDataStore(IWeatherService weatherService, IRecalculateFlightEventPublisher recalculateFlightEventPublisher)
        {
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
            _flightRecalculation = recalculateFlightEventPublisher ?? throw new ArgumentNullException(nameof(recalculateFlightEventPublisher));
        }

        public async Task StartAsync()
        {
            _container = new PostgreSqlBuilder().Build();
            await _container.StartAsync();

            var contextOptionsBuilder = new DbContextOptionsBuilder<SimplePostgreSQLDbContext>();
            contextOptionsBuilder.UseNpgsql(_container.GetConnectionString());
            _dbContext = new SimplePostgreSQLDbContext(contextOptionsBuilder.Options);

            await _dbContext.Database.MigrateAsync();
            Console.WriteLine("Started and migrated database");
#if DEBUG
            Console.WriteLine("Simple Postgres Datastore ConnectionString: " + _container.GetConnectionString());
#endif
        }

        public async Task AddOrUpdateFlightAsync(Flight flight)
        {
            if (_dbContext is null)
            {
                throw new InvalidOperationException("Database is not ready");
            }

            var weather = GetWeatherCategoriesForFlight(flight);
            if (await _dbContext.Flights.FirstOrDefaultAsync(x => x.FlightIdentification == flight.FlightIdentification) is { } foundFlight)
            {
                // Update the flight in the datastore
                foundFlight.IsRecalculating = false;
                foreach (var airport in foundFlight.Airports)
                {
                    airport.LastSeenWeatherCategory = weather.GetValueOrDefault(airport.ICAO, WeatherCategory.Undefined);
                }
            }
            else
            {
                // Add the flight 
                var flightEntity = new DbEntities.FlightEntity()
                {
                    FlightIdentification = flight.FlightIdentification,
                    ScheduledTimeOfDeparture = flight.ScheduledTimeOfDeparture,
                    ScheduledTimeOfArrival = flight.ScheduledTimeOfArrival,
                    IsRecalculating = false
                };
                flightEntity.Airports.Add(new DbEntities.AirportEntity()
                {
                    ICAO = flight.DepartureAirport,
                    LastSeenWeatherCategory = weather.GetValueOrDefault(flight.DepartureAirport, WeatherCategory.Undefined)
                });
                flightEntity.Airports.Add(new DbEntities.AirportEntity()
                {
                    ICAO = flight.DestinationAirport,
                    LastSeenWeatherCategory = weather.GetValueOrDefault(flight.DestinationAirport, WeatherCategory.Undefined)
                });
                foreach (var airport in flight.OtherRelatedAirports)
                {
                    flightEntity.Airports.Add(new DbEntities.AirportEntity()
                    {
                        ICAO = airport.Key,
                        LastSeenWeatherCategory = weather.GetValueOrDefault(airport.Key, WeatherCategory.Undefined)
                    });
                }
                _dbContext.Flights.Add(flightEntity);
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task AddWeatherAsync(Weather weather)
        {
            if (_dbContext is null)
            {
                throw new InvalidOperationException("Database is not ready");
            }

            var affectedFlights = await _dbContext.Airports
                .Where(x => x.ICAO == weather.Airport  // Get all airports matching the weather
                        && !x.FlightEntity.IsRecalculating // Whose flights are not already being recalculated
                        && (x.FlightEntity.ScheduledTimeOfArrival < weather.ValidFrom // And whose flights overlap with the weather
                            || weather.ValidTo < x.FlightEntity.ScheduledTimeOfDeparture)
                          && x.LastSeenWeatherCategory < weather.WeatherLevel) // And those where the current wetter is now worse
                .ToListAsync();

            foreach (var airport in affectedFlights)
            {
                var flight = airport.FlightEntity;
                // We need to recalculate
                Console.WriteLine($"Recalculate flight: {flight.FlightIdentification}");
                flight.IsRecalculating = true;

                await _flightRecalculation.PublishRecalculationAsync(flight.FlightIdentification).ConfigureAwait(false);
            }
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteFlightAsync(string id)
        {
            if (_dbContext is null)
            {
                throw new InvalidOperationException("Database is not ready");
            }
            var flight = new FlightEntity() { FlightIdentification = id };
            _dbContext.Flights.Remove(flight);
            await _dbContext.SaveChangesAsync();
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

        public async Task ResetAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync().AsTask();
            }
            if (_dbContext is not null)
            {
                await _dbContext.DisposeAsync().AsTask();
            }
            await StartAsync();
        }

        public void Dispose()
        {
            if (_container is not null)
            {
                _container.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _container = null;
            }
            if (_dbContext is not null)
            {
                _dbContext.Dispose();
                _dbContext = null;
            }
        }
    }
}
