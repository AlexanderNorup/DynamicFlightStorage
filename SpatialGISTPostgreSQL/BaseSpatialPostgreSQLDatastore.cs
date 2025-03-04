using DynamicFlightStorageDTOs;
using Npgsql;
using Testcontainers.PostgreSql;

namespace SpatialGISTPostgreSQL;

public abstract class BaseSpatialPostgreSQLDatastore : IEventDataStore, IDisposable
{
    private PostgreSqlContainer? _container;
    private NpgsqlConnection? _insertConnection;
    private NpgsqlConnection? _updateConnection;
    private readonly IWeatherService _weatherService;
    private readonly IRecalculateFlightEventPublisher _flightRecalculation;
    private readonly string _initScriptPath;
    
    public BaseSpatialPostgreSQLDatastore(IWeatherService weatherService, IRecalculateFlightEventPublisher recalculateFlightEventPublisher, string initScriptName)
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
    
    public Task AddOrUpdateFlightAsync(Flight flight)
    {
        throw new NotImplementedException();
    }

    public Task DeleteFlightAsync(string id)
    {
        throw new NotImplementedException();
    }

    public Task AddWeatherAsync(Weather weather)
    {
        throw new NotImplementedException();
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