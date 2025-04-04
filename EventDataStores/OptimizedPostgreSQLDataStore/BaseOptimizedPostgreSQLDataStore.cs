﻿using DynamicFlightStorageDTOs;
using Npgsql;
using Testcontainers.PostgreSql;

namespace OptimizedPostgreSQLDataStore
{
    public abstract class BaseOptimizedPostgreSQLDataStore : IEventDataStore, IDisposable
    {
        private PostgreSqlContainer? _container;
        private NpgsqlConnection? _insertConnection;
        private NpgsqlConnection? _updateConnection;
        private readonly IWeatherService _weatherService;
        private readonly IRecalculateFlightEventPublisher _flightRecalculation;
        private readonly string _initScriptPath;
        public BaseOptimizedPostgreSQLDataStore(IWeatherService weatherService, IRecalculateFlightEventPublisher recalculateFlightEventPublisher, string initScriptName)
        {
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
            _flightRecalculation = recalculateFlightEventPublisher ?? throw new ArgumentNullException(nameof(recalculateFlightEventPublisher));
            _initScriptPath = Path.Combine(AppContext.BaseDirectory, "DatabaseInit", initScriptName ?? throw new ArgumentNullException(nameof(initScriptName)));
            if (!File.Exists(_initScriptPath))
            {
                throw new ArgumentException("Init script not found at " + _initScriptPath, nameof(initScriptName));
            }
        }

        public async Task StartAsync()
        {
            var builder = new PostgreSqlBuilder();
            if (Environment.GetEnvironmentVariable("ENABLE_TEMPFS") is not null)
            {
                builder = builder.WithTmpfsMount("/var/lib/postgresql/data");
            }
            _container = builder.Build();
            await _container.StartAsync();

            var initScript = await File.ReadAllTextAsync(_initScriptPath);
            await _container.ExecScriptAsync(initScript);

            var insertConn = new NpgsqlConnection(_container.GetConnectionString());
            await insertConn.OpenAsync();
            _insertConnection = insertConn;

            var updateConn = new NpgsqlConnection(_container.GetConnectionString());
            await updateConn.OpenAsync();
            _updateConnection = updateConn;

            Console.WriteLine($"Started PostgresSQL database with script {Path.GetFileName(_initScriptPath)}");
#if DEBUG
            Console.WriteLine($"PostgresSQL ConnectionString: {_container.GetConnectionString()}");
#endif
        }

        public async Task ResetAsync()
        {
            if (_insertConnection is not null)
            {
                await _insertConnection.CloseAsync();
                await _insertConnection.DisposeAsync().AsTask();
            }
            if (_updateConnection is not null)
            {
                await _updateConnection.CloseAsync();
                await _updateConnection.DisposeAsync().AsTask();
            }
            if (_container is not null)
            {
                await _container.DisposeAsync().AsTask();
            }
            await StartAsync();
        }

        public async Task AddOrUpdateFlightAsync(Flight flight)
        {
            await using (var batch = new NpgsqlBatch(_insertConnection))
            {
                // Insert the flight
                const string InsertFlightSql =
                """
                INSERT INTO flights (flightIdentification,departureTime,arrivalTime)
                VALUES
                  ($1,$2,$3)
                ON CONFLICT (flightidentification) DO UPDATE SET isrecalculating = false;
                """;

                var flightCmd = new NpgsqlBatchCommand(InsertFlightSql);
                flightCmd.Parameters.AddWithValue(flight.FlightIdentification);
                flightCmd.Parameters.AddWithValue(flight.ScheduledTimeOfDeparture);
                flightCmd.Parameters.AddWithValue(flight.ScheduledTimeOfArrival);

                batch.BatchCommands.Add(flightCmd);

                var weather = _weatherService.GetWeatherCategoriesForFlight(flight);

                foreach (var airport in flight.GetAllAirports().Distinct())
                {
                    // Insert the airport
                    const string InsertAirportSql =
                    """
                    INSERT INTO airports (icao, flightidentification, lastseenweather)
                    VALUES
                     ($1, $2, $3)
                    ON CONFLICT (icao, flightidentification) DO UPDATE SET lastseenweather = EXCLUDED.lastseenweather;
                    """;

                    var batchCmd = new NpgsqlBatchCommand(InsertAirportSql);
                    batchCmd.Parameters.AddWithValue(airport);
                    batchCmd.Parameters.AddWithValue(flight.FlightIdentification);
                    batchCmd.Parameters.AddWithValue((int)weather.GetValueOrDefault(airport, WeatherCategory.Undefined));
                    batch.BatchCommands.Add(batchCmd);
                }
                await batch.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task AddWeatherAsync(Weather weather, DateTime recievedTime)
        {
            const string SearchSql =
                """
                SELECT a.flightIdentification FROM Airports a
                INNER JOIN Flights f on f.flightIdentification = a.flightIdentification
                WHERE a.icao = $1
                    AND a.lastSeenWeather < $2
                    AND f.isRecalculating = false
                    AND f.departureTime <= $3
                    AND $4 <= f.arrivalTime
                """;

            const string UpdateRecalculatingSql =
                """
                UPDATE flights SET isRecalculating = true
                WHERE flightidentification = $1;
                """;

            await using (var updateBatch = new NpgsqlBatch(_updateConnection))
            {
                await using (var cmd = new NpgsqlCommand(SearchSql, _updateConnection)
                {
                    Parameters = {
                        new() { Value = weather.Airport },
                        new() { Value = (int)weather.WeatherLevel },
                        new() { Value = weather.ValidTo },
                        new() { Value = weather.ValidFrom },
                    }
                })
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var flightId = reader.GetString(0);
                        await _flightRecalculation.PublishRecalculationAsync(flightId, weather.Id, DateTime.UtcNow - recievedTime);
                        updateBatch.BatchCommands.Add(new NpgsqlBatchCommand(UpdateRecalculatingSql)
                        {
                            Parameters = { new() { Value = flightId } }
                        });
                    }
                }
                if (updateBatch.BatchCommands.Count > 0)
                {
                    await updateBatch.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task DeleteFlightAsync(string id)
        {
            const string DeleteSql =
                """
                DELETE FROM flights WHERE flightidentification = $1;
                """;
            // TODO: Make a _deleteConnection if this is actually needed.
            await using (var cmd = new NpgsqlCommand(DeleteSql, _insertConnection))
            {
                cmd.Parameters.AddWithValue(id);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public void Dispose()
        {
            if (_insertConnection is not null)
            {
                _insertConnection.CloseAsync();
                _insertConnection.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _insertConnection = null;
            }
            if (_updateConnection is not null)
            {
                _updateConnection.CloseAsync();
                _updateConnection.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _updateConnection = null;
            }
            if (_container is not null)
            {
                _container.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _container = null;
            }
        }
    }
}