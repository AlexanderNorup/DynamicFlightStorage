using DynamicFlightStorageDTOs;
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
            _container = new PostgreSqlBuilder().Build();
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
                  (@id,@dep,@arr)
                ON CONFLICT (flightidentification) DO UPDATE SET isrecalculating = false;
                """;

                var flightCmd = new NpgsqlBatchCommand(InsertFlightSql);
                flightCmd.Parameters.AddWithValue("id", flight.FlightIdentification);
                flightCmd.Parameters.AddWithValue("dep", flight.ScheduledTimeOfDeparture);
                flightCmd.Parameters.AddWithValue("arr", flight.ScheduledTimeOfArrival);

                batch.BatchCommands.Add(flightCmd);

                var weather = _weatherService.GetWeatherCategoriesForFlight(flight);

                foreach (var airport in flight.GetAllAirports().Distinct())
                {
                    // Insert the airport
                    const string InsertAirportSql =
                    """
                    INSERT INTO airports (icao, flightidentification, lastseenweather)
                    VALUES
                     (@icao, @flightIdent, @lastSeenWeather)
                    ON CONFLICT (icao, flightidentification) DO UPDATE SET lastseenweather = EXCLUDED.lastseenweather;
                    """;

                    var batchCmd = new NpgsqlBatchCommand(InsertAirportSql);
                    batchCmd.Parameters.AddWithValue("icao", airport);
                    batchCmd.Parameters.AddWithValue("flightIdent", flight.FlightIdentification);
                    batchCmd.Parameters.AddWithValue("lastSeenWeather", (int)weather.GetValueOrDefault(airport, WeatherCategory.Undefined));
                    batch.BatchCommands.Add(batchCmd);
                }
                await batch.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task AddWeatherAsync(Weather weather)
        {
            const string SearchSql =
                """
                SELECT a.flightIdentification FROM Airports a
                INNER JOIN Flights f on f.flightIdentification = a.flightIdentification
                WHERE a.icao = @icao
                    AND a.lastSeenWeather < @newWeather
                    AND f.isRecalculating = false
                    AND (f.arrivalTime < @validFrom
                        OR @validTo < f.departureTime)
                """;

            const string UpdateRecalculatingSql =
                """
                UPDATE flights SET isRecalculating = true
                WHERE flightidentification = @id;
                """;

            await using (var updateBatch = new NpgsqlBatch(_updateConnection))
            {
                await using (var cmd = new NpgsqlCommand(SearchSql, _updateConnection)
                {
                    Parameters = {
                        new("icao", weather.Airport),
                        new("newWeather", (int)weather.WeatherLevel),
                        new("validFrom", weather.ValidFrom),
                        new("validTo", weather.ValidTo),
                    }
                })
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var flightId = reader.GetString(0);
                        await _flightRecalculation.PublishRecalculationAsync(flightId);
                        updateBatch.BatchCommands.Add(new NpgsqlBatchCommand(UpdateRecalculatingSql)
                        {
                            Parameters = { new("id", flightId) }
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
                DELETE FROM flights WHERE flightidentification = @id;
                """;
            // TODO: Make a _deleteConnection if this is actually needed.
            await using (var cmd = new NpgsqlCommand(DeleteSql, _insertConnection))
            {
                cmd.Parameters.AddWithValue("id", id);
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