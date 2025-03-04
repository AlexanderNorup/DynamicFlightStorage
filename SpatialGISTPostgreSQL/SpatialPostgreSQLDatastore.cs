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
                int icaoNum = ConvertIcaoToInt2(airport);
                const string insertFlightEventSql =
                    """
                    INSERT INTO flight_events (flightIdentification, lastWeather, departure, arrival, icao, line3d)
                    VALUES (
                        @flightId,
                        @weather,
                        @departure,
                        @arrival,
                        @icao,
                        cube(ARRAY[@weather, @departureEpoch, @icaoNum],
                            ARRAY[@weather, @arrivalEpoch, @icaoNum])
                    )
                    ON CONFLICT (icao, flightIdentification)  
                    DO UPDATE SET 
                        lastWeather = EXCLUDED.lastWeather,
                        isRecalculating = FALSE,
                        line3d = cube(ARRAY[EXCLUDED.lastWeather, @departureEpoch, @icaoNum],
                                      ARRAY[EXCLUDED.lastWeather, @arrivalEpoch, @icaoNum]);
                        
                    """;
                var batchCmd = new NpgsqlBatchCommand(insertFlightEventSql);
                batchCmd.Parameters.AddWithValue("flightId", flight.FlightIdentification);
                batchCmd.Parameters.AddWithValue("weather", (int)weather.GetValueOrDefault(airport, WeatherCategory.Undefined));
                batchCmd.Parameters.AddWithValue("departure", flight.ScheduledTimeOfDeparture);
                batchCmd.Parameters.AddWithValue("arrival", flight.ScheduledTimeOfArrival);
                batchCmd.Parameters.AddWithValue("icao", airport);
                batchCmd.Parameters.AddWithValue("departureEpoch", departureEpoch);
                batchCmd.Parameters.AddWithValue("arrivalEpoch", arrivalEpoch);
                batchCmd.Parameters.AddWithValue("icaoNum", icaoNum);
                batch.BatchCommands.Add(batchCmd);
            }
            await batch.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public async Task DeleteFlightAsync(string id)
    {
        const string deleteSql =
            """
            DELETE FROM flight_events WHERE flightIdentificaiton = @id;
            """;
        await using (var cmd = new NpgsqlCommand(deleteSql, _insertConnection))
        {
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public async Task AddWeatherAsync(Weather weather)
    {
        int icaoNum = ConvertIcaoToInt2(weather.Airport);
        var departureEpoch = ((DateTimeOffset)weather.ValidFrom).ToUnixTimeSeconds();
        var arrivalEpoch = ((DateTimeOffset)weather.ValidTo).ToUnixTimeSeconds();
        int weatherMin = (int)WeatherCategory.Undefined;
        int weatherMax = (int)weather.WeatherLevel - 1;
        const string searchSql =
            """
            SELECT DISTINCT ON (flightIdentification) * 
            FROM flight_events
            WHERE line3d && cube(ARRAY[@weatherMin, @departureEpoch, @icaoNum],
                                 ARRAY[@weatherMax, @arrivalEpoch, @icaoNum])
            AND isRecalculating = FALSE;
            """;
        const string updateRecalculatingSql = 
            """
            UPDATE flight_events SET isRecalculating = TRUE
            WHERE flightIdentification = @id AND isRecalculating = FALSE;
            """;
        await using (var updateBatch = new NpgsqlBatch(_updateConnection))
        {
            await using (var cmd = new NpgsqlCommand(searchSql, _updateConnection) 
            {
                Parameters = {
                    new ("weatherMin", weatherMin),
                    new ("departureEpoch", departureEpoch),
                    new ("icaoNum", icaoNum),
                    new ("weatherMax", weatherMax),
                    new ("arrivalEpoch", arrivalEpoch)
                }
            })
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var flightId = reader.GetString(0);
                    await _flightRecalculation.PublishRecalculationAsync(flightId);
                    updateBatch.BatchCommands.Add(new NpgsqlBatchCommand(updateRecalculatingSql)
                    {
                        Parameters = { new ("id", flightId) }
                    });
                }
            }

            if (updateBatch.BatchCommands.Count > 0)
            {
                await updateBatch.ExecuteNonQueryAsync();
            }
        }
    }
    
    private int ConvertIcaoToInt(string icao)
    {
        int icaoNum = 0;
        for (int i = 0; i < 4; i++)
        {
            try
            {
                icaoNum += icao[i] * (int)Math.Pow(10, i);
            }
            catch (IndexOutOfRangeException e)
            {
                continue;
            }
        }

        return icaoNum;
    }
    
    private int ConvertIcaoToInt2(string icao)
    {
        // Ensure ICAO is at most 4 characters
        if (icao.Length > 4)
            throw new ArgumentException($"ICAO code must be at most 4 characters long. Given: {icao}", nameof(icao));

        // Pad ICAO with spaces (' ') if it's shorter than 4 characters
        icao = icao.PadRight(4, ' '); // Ensures all ICAOs are exactly 4 characters

        // Encode ICAO into an integer
        return (icao[0] << 24) | (icao[1] << 16) | (icao[2] << 8) | icao[3];
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