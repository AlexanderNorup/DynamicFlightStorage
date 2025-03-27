using DynamicFlightStorageDTOs;
using Npgsql;
using Testcontainers.PostgreSql;

namespace SpatialGISTPostgreSQL;

public class SpatialPostgreSQLDatastore : IEventDataStore, IDisposable
{
    private PostgreSqlContainer? _container;
    private NpgsqlConnection? _insertConnection;
    private NpgsqlConnection? _updateConnection;
    private readonly IWeatherService _weatherService;
    private readonly IRecalculateFlightEventPublisher _flightRecalculation;
    private readonly string _initScriptPath;

    public SpatialPostgreSQLDatastore(IWeatherService weatherService, IRecalculateFlightEventPublisher recalculateFlightEventPublisher)
    {
        var initScriptName = "gistInit.sql";
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
            var departureEpoch = ((DateTimeOffset)flight.ScheduledTimeOfDeparture).ToUnixTimeSeconds();
            var arrivalEpoch = ((DateTimeOffset)flight.ScheduledTimeOfArrival).ToUnixTimeSeconds();
            var weather = _weatherService.GetWeatherCategoriesForFlight(flight);
            foreach (var airport in flight.GetAllAirports().Distinct())
            {
                int icaoNum = IcaoConversionHelper.ConvertIcaoToInt(airport);
                const string insertFlightEventSql =
                    """
                    INSERT INTO flight_events (flightIdentification, lastWeather, departure, arrival, icao, line3d)
                    VALUES (
                        $1,
                        $2,
                        $3,
                        $4,
                        $5,
                        cube(ARRAY[$2, $6, $7],
                            ARRAY[$2, $8, $7])
                    )
                    ON CONFLICT (flightIdentification, icao)  
                    DO UPDATE SET 
                        lastWeather = EXCLUDED.lastWeather,
                        isRecalculating = FALSE,
                        line3d = cube(ARRAY[EXCLUDED.lastWeather, $6, $7],
                                      ARRAY[EXCLUDED.lastWeather, $8, $7]);
                        
                    """;
                var batchCmd = new NpgsqlBatchCommand(insertFlightEventSql);
                batchCmd.Parameters.AddWithValue(flight.FlightIdentification);
                batchCmd.Parameters.AddWithValue((int)weather.GetValueOrDefault(airport, WeatherCategory.Undefined));
                batchCmd.Parameters.AddWithValue(flight.ScheduledTimeOfDeparture);
                batchCmd.Parameters.AddWithValue(flight.ScheduledTimeOfArrival);
                batchCmd.Parameters.AddWithValue(airport);
                batchCmd.Parameters.AddWithValue(departureEpoch);
                batchCmd.Parameters.AddWithValue(icaoNum);
                batchCmd.Parameters.AddWithValue(arrivalEpoch);
                batch.BatchCommands.Add(batchCmd);
            }
            await batch.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public async Task DeleteFlightAsync(string id)
    {
        const string deleteSql =
            """
            DELETE FROM flight_events WHERE flightIdentification = $1;
            """;
        await using (var cmd = new NpgsqlCommand(deleteSql, _insertConnection))
        {
            cmd.Parameters.AddWithValue(id);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public async Task AddWeatherAsync(Weather weather, DateTime recievedTime)
    {
        int icaoNum = IcaoConversionHelper.ConvertIcaoToInt(weather.Airport);
        var departureEpoch = ((DateTimeOffset)weather.ValidFrom).ToUnixTimeSeconds();
        var arrivalEpoch = ((DateTimeOffset)weather.ValidTo).ToUnixTimeSeconds();
        int weatherMin = (int)WeatherCategory.Undefined;
        int weatherMax = (int)weather.WeatherLevel - 1;
        const string searchSql =
            """
            SELECT DISTINCT ON (flightIdentification) flightIdentification
            FROM flight_events
            WHERE line3d && cube(ARRAY[$1, $2, $3],
                                 ARRAY[$4, $5, $3])
            AND isRecalculating = FALSE;
            """;
        const string updateRecalculatingSql =
            """
            UPDATE flight_events SET isRecalculating = TRUE
            WHERE flightIdentification = $1 AND isRecalculating = FALSE;
            """;
        await using (var updateBatch = new NpgsqlBatch(_updateConnection))
        {
            await using (var cmd = new NpgsqlCommand(searchSql, _updateConnection)
            {
                Parameters = {
                    new () { Value = weatherMin },
                    new () { Value = departureEpoch },
                    new () { Value = icaoNum },
                    new () { Value = weatherMax },
                    new () { Value = arrivalEpoch }
                }
            })
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var flightId = reader.GetString(0);
                    await _flightRecalculation.PublishRecalculationAsync(flightId, weather.Id, DateTime.UtcNow - recievedTime);
                    updateBatch.BatchCommands.Add(new NpgsqlBatchCommand(updateRecalculatingSql)
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