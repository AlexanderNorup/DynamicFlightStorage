using System.Text.RegularExpressions;
using DynamicFlightStorageDTOs;
using Npgsql;
using Testcontainers.PostgreSql;

namespace SpatialGISTManyTablesPostgreSQLDataStore;

public partial class SpatialGISTManyTablesPostgreSQLDataStore : IEventDataStore, IDisposable
{
    private PostgreSqlContainer? _container;
    private NpgsqlConnection? _insertConnection;
    private NpgsqlConnection? _updateConnection;
    private readonly IWeatherService _weatherService;
    private readonly IRecalculateFlightEventPublisher _flightRecalculation;
    private readonly string _initScriptPath;
    private Dictionary<string, HashSet<string>> _icaoDictionary;
    private HashSet<string> _tableSet;
    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex SanitizeAirport();

    public SpatialGISTManyTablesPostgreSQLDataStore(IWeatherService weatherService,
        IRecalculateFlightEventPublisher recalculateFlightEventPublisher)
    {
        var initScriptName = "manyGistInit.sql";
        _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
        _flightRecalculation = recalculateFlightEventPublisher ?? throw new ArgumentNullException(nameof(recalculateFlightEventPublisher));
        _initScriptPath = Path.Combine(AppContext.BaseDirectory, "DatabaseInit", initScriptName ?? throw new ArgumentNullException(nameof(initScriptName)));
        if (!File.Exists(_initScriptPath))
        {
            throw new ArgumentException("Init script not found at " + _initScriptPath, nameof(_initScriptPath));
        }
        _icaoDictionary = new Dictionary<string, HashSet<string>>();
        _tableSet = new HashSet<string>();
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
            foreach (var airport in flight.GetAllAirports().Distinct())
            {
                var cleanAirport = SanitizeAirport().Replace(airport, "");
                if (_tableSet.Contains(cleanAirport)) continue;

                string createTableSql =
                    $"""
                     CREATE TABLE "{cleanAirport}_table" (
                         flightIdentification VARCHAR(36) UNIQUE PRIMARY KEY NOT NULL,
                         isRecalculating BOOL NOT NULL DEFAULT (FALSE),
                         lastWeather INT,
                         departure TIMESTAMP NOT NULL,
                         arrival TIMESTAMP NOT NULL,
                         line2d CUBE NOT NULL
                     );
                     """;
                var batchCmd0 = new NpgsqlBatchCommand(createTableSql);
                batch.BatchCommands.Add(batchCmd0);


                string createIndexSql1 =
                    $"""
                     CREATE INDEX {cleanAirport}_events_gist_idx
                     ON "{cleanAirport}_table" USING GIST (line2d);
                     """;
                var batchCmd1 = new NpgsqlBatchCommand(createIndexSql1);
                batch.BatchCommands.Add(batchCmd1);

                string createIndexSql2 =
                    $"""
                     CREATE INDEX {cleanAirport}_recalc_idx
                     ON "{cleanAirport}_table" (flightIdentification, isRecalculating) WHERE isRecalculating = FALSE;
                     """;
                var batchCmd2 = new NpgsqlBatchCommand(createIndexSql2);
                batch.BatchCommands.Add(batchCmd2);

                _tableSet.Add(cleanAirport);
            }

            await batch.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (var batch = new NpgsqlBatch(_insertConnection))
        {
            var departureEpoch = ((DateTimeOffset)flight.ScheduledTimeOfDeparture).ToUnixTimeSeconds();
            var arrivalEpoch = ((DateTimeOffset)flight.ScheduledTimeOfArrival).ToUnixTimeSeconds();
            var weather = _weatherService.GetWeatherCategoriesForFlight(flight);
            foreach (var airport in flight.GetAllAirports().Distinct())
            {
                var cleanAirport = SanitizeAirport().Replace(airport, "");
                string insertFlightEventSql =
                    $"""
                    INSERT INTO "{cleanAirport}_table" (flightIdentification, lastWeather, departure, arrival, line2d)
                    VALUES (
                        $1,
                        $2,
                        $3,
                        $4,
                        cube(ARRAY[$2, $5], ARRAY[$2, $6])
                    )
                    ON CONFLICT (flightIdentification)  
                    DO UPDATE SET 
                        lastWeather = EXCLUDED.lastWeather,
                        isRecalculating = FALSE;
                    """;

                var batchCmd = new NpgsqlBatchCommand(insertFlightEventSql);
                batchCmd.Parameters.AddWithValue(flight.FlightIdentification);
                batchCmd.Parameters.AddWithValue((int)weather.GetValueOrDefault(airport, WeatherCategory.Undefined));
                batchCmd.Parameters.AddWithValue(flight.ScheduledTimeOfDeparture);
                batchCmd.Parameters.AddWithValue(flight.ScheduledTimeOfArrival);
                batchCmd.Parameters.AddWithValue(departureEpoch);
                batchCmd.Parameters.AddWithValue(arrivalEpoch);
                batch.BatchCommands.Add(batchCmd);
            }
            await batch.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        if (!_icaoDictionary.ContainsKey(flight.FlightIdentification))
        {
            _icaoDictionary.Add(flight.FlightIdentification, new HashSet<string>());
        }
        foreach (var icao in flight.GetAllAirports().Distinct())
        {
            _icaoDictionary[flight.FlightIdentification].Add(icao);
        }
    }

    public async Task DeleteFlightAsync(string id)
    {
        if (!_icaoDictionary.ContainsKey(id)) return;
        await using (var batch = new NpgsqlBatch(_insertConnection))
        {
            foreach (var icao in _icaoDictionary[id])
            {
                var deleteSql =
                    $"""
                    DELETE FROM "{icao}_table" WHERE flightIdentification = $1;
                    """;
                var batchCmd = new NpgsqlBatchCommand(deleteSql);
                batchCmd.Parameters.AddWithValue(id);
                batch.BatchCommands.Add(batchCmd);
            }
            await batch.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        _icaoDictionary.Remove(id);
    }

    public async Task AddWeatherAsync(Weather weather, DateTime recievedTime)
    {
        var cleanWeatherAirport = SanitizeAirport().Replace(weather.Airport, "");
        if (!_tableSet.Contains(cleanWeatherAirport)) return;
        var departureEpoch = ((DateTimeOffset)weather.ValidFrom).ToUnixTimeSeconds();
        var arrivalEpoch = ((DateTimeOffset)weather.ValidTo).ToUnixTimeSeconds();
        int weatherMin = (int)WeatherCategory.Undefined;
        int weatherMax = (int)weather.WeatherLevel - 1;
        string searchSql =
            $"""
            SELECT DISTINCT ON (flightIdentification) flightIdentification
            FROM "{cleanWeatherAirport}_table"
            WHERE line2d && cube(ARRAY[$1, $2],
                                ARRAY[$3, $4])
            AND isRecalculating = FALSE;
            """;

        await using (var updateBatch = new NpgsqlBatch(_updateConnection))
        {
            await using (var cmd = new NpgsqlCommand(searchSql, _updateConnection)
            {
                Parameters = {
                                 new () { Value = weatherMin },
                                 new () { Value = departureEpoch },
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

                    foreach (var airport in _icaoDictionary[flightId])
                    {
                        var cleanAirport = SanitizeAirport().Replace(airport, "");
                        string updateRecalculatingSql =
                            $"""
                             UPDATE "{cleanAirport}_table" SET isRecalculating = TRUE
                             WHERE flightIdentification = $1 AND isRecalculating = FALSE;
                             """;
                        updateBatch.BatchCommands.Add(new NpgsqlBatchCommand(updateRecalculatingSql)
                        {
                            Parameters =
                            {
                                new () { Value = flightId }
                            }
                        });
                    }
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