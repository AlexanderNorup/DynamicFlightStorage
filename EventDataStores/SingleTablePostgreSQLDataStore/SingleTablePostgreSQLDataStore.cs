using DynamicFlightStorageDTOs;
using Npgsql;
using Testcontainers.PostgreSql;

namespace SingleTablePostgreSQLDataStore;

public class SingleTablePostgreSQLDataStore : IEventDataStore, IDisposable
{
    private PostgreSqlContainer? _container;
    private NpgsqlConnection? _insertConnection;
    private NpgsqlConnection? _updateConnection;
    private readonly IWeatherService _weatherService;
    private readonly IRecalculateFlightEventPublisher _flightRecalculation;
    private readonly string _initScriptPath;

    public SingleTablePostgreSQLDataStore(IWeatherService weatherService, IRecalculateFlightEventPublisher recalculateFlightEventPublisher)
    {
        var initScriptName = "singleTableInit.sql";
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
            var weather = _weatherService.GetWeatherCategoriesForFlight(flight);
            foreach (var airport in flight.GetAllAirports().Distinct())
            {
                const string insertFlightEventSql =
                    """
                    INSERT INTO flight_events (flightIdentification, lastWeather, departure, arrival, icao)
                    VALUES (
                        $1,
                        $2,
                        $3,
                        $4,
                        $5
                    )
                    ON CONFLICT (flightIdentification, icao)  
                    DO UPDATE SET 
                        lastWeather = EXCLUDED.lastWeather,
                        isRecalculating = FALSE;
                    """;
                var batchCmd = new NpgsqlBatchCommand(insertFlightEventSql);
                batchCmd.Parameters.AddWithValue(flight.FlightIdentification);
                batchCmd.Parameters.AddWithValue((int)weather.GetValueOrDefault(airport, WeatherCategory.Undefined));
                batchCmd.Parameters.AddWithValue(flight.ScheduledTimeOfDeparture);
                batchCmd.Parameters.AddWithValue(flight.ScheduledTimeOfArrival);
                batchCmd.Parameters.AddWithValue(airport);
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
        int newWeather = (int)weather.WeatherLevel;
        const string searchSql =
            """
            SELECT DISTINCT ON (flightIdentification) flightIdentification 
            FROM flight_events
            WHERE lastWeather < $1
            AND icao = $2
            AND isRecalculating = FALSE
            AND departure <= $3
            AND arrival >= $4;
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
                    new () { Value = newWeather },
                    new () { Value = weather.Airport },
                    new () { Value = weather.ValidTo },
                    new () { Value = weather.ValidFrom },
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