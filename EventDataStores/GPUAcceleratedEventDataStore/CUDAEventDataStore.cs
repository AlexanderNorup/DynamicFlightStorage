using DynamicFlightStorageDTOs;
using MessagePack;
using System.Collections.Concurrent;

namespace GPUAcceleratedEventDataStore
{
    public class CUDAEventDataStore : IEventDataStore, IDisposable
    {
        public static readonly MessagePackSerializerOptions MessagePackOptions = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
        public static readonly string PersistPath = Path.Combine(AppContext.BaseDirectory, "CudaDataStorePersist", "CudaFlights.bin");
        private static readonly TimeSpan PersistInterval = TimeSpan.FromMinutes(15);
        private SemaphoreSlim _persistSemaphore;
        private System.Timers.Timer _persistTimer;
        private ConcurrentBag<Flight> _persistBag;
        private bool _persistBagDirty = false;

        private bool disposedValue;
        private CudaFlightSystem? _cudaFlightSystem;
        private readonly IWeatherService _weatherService;
        private readonly IRecalculateFlightEventPublisher _flightRecalculation;

        private int _nextFlightId = 0;
        private readonly Dictionary<string, int> _flightIdentToIdMap = new();
        private readonly Dictionary<int, string> _flightIdToIdentMap = new();
        public CUDAEventDataStore(IWeatherService weatherService, IRecalculateFlightEventPublisher recalculateFlightEventPublisher)
        {
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
            _flightRecalculation = recalculateFlightEventPublisher ?? throw new ArgumentNullException(nameof(recalculateFlightEventPublisher));

            _persistBag = new ConcurrentBag<Flight>();
            _persistSemaphore = new SemaphoreSlim(1, 1);
            _persistTimer = new System.Timers.Timer();
            _persistTimer.Elapsed += async (o, s) => await PersistToDisk().ConfigureAwait(false);
            _persistTimer.AutoReset = true;
            _persistTimer.Interval = PersistInterval.TotalMilliseconds;
        }

        public Task AddOrUpdateFlightAsync(Flight flight)
        {
            if (_cudaFlightSystem is null)
            {
                throw new InvalidOperationException("CUDA Flight System is not ready");
            }

            var weather = _weatherService.GetWeatherCategoriesForFlight(flight);

            if (_flightIdentToIdMap.TryGetValue(flight.FlightIdentification, out var id))
            {
                // Exists
                _cudaFlightSystem.UpdateFlights(new GPUFlight(flight, id, weather));
            }
            else
            {
                // Does not exist already
                int newId = CreateNewFlightId(flight);
                _cudaFlightSystem.AddFlights(new GPUFlight(flight, newId, weather));
            }
            return Task.CompletedTask;
        }

        public async Task AddWeatherAsync(Weather weather, DateTime recievedTime)
        {
            if (_cudaFlightSystem is null)
            {
                throw new InvalidOperationException("CUDA Flight System is not ready");
            }

            int[] affectedFlights = _cudaFlightSystem.FindFlightsAffectedByWeather(weather);
            foreach (var flightId in affectedFlights)
            {
                if (_flightIdToIdentMap.TryGetValue(flightId, out var ident))
                {
                    await _flightRecalculation.PublishRecalculationAsync(ident, weather.Id, DateTime.UtcNow - recievedTime);
                }
                else
                {
                    Console.WriteLine($"Trying to recalculate flight with id {flightId}, but a corrosponding flight identification was not found");
                }
            }
        }

        public Task DeleteFlightAsync(string id)
        {
            if (_cudaFlightSystem is null)
            {
                throw new InvalidOperationException("CUDA Flight System is not ready");
            }
            if (_flightIdentToIdMap.TryGetValue(id, out var id_))
            {
                _cudaFlightSystem.RemoveFlights(id_);

                _flightIdentToIdMap.Remove(id);
                _flightIdToIdentMap.Remove(id_);
            }
            return Task.CompletedTask;
        }

        public Task ResetAsync()
        {
            _persistTimer.Stop();
            if (_cudaFlightSystem is not null)
            {
                _cudaFlightSystem.Dispose();
            }
            _persistBag.Clear();
            _flightIdentToIdMap.Clear();
            _flightIdToIdentMap.Clear();
            _nextFlightId = 0;
            return StartAsync();
        }

        public Task StartAsync()
        {
            _cudaFlightSystem = new CudaFlightSystem();
            _cudaFlightSystem.Initialize();
            // Purposfully chose not to load the persisted data for the experiments.
            //await LoadFromPersistedData().ConfigureAwait(false);
            _persistTimer.Start();
            return Task.CompletedTask;
        }

        private int CreateNewFlightId(Flight flight)
        {
            int id = _nextFlightId++;
            _flightIdToIdentMap.Add(id, flight.FlightIdentification);
            _flightIdentToIdMap.Add(flight.FlightIdentification, id);
            _persistBag.Add(flight);
            _persistBagDirty = true;

            return id;
        }

        private async Task PersistToDisk()
        {
            if (_persistBag.IsEmpty || !_persistBagDirty)
            {
                return;
            }

            await _persistSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_persistBagDirty)
                {
                    return;
                }
                var flights = _persistBag.ToArray();
                _persistBagDirty = false;
                Directory.CreateDirectory(Path.GetDirectoryName(PersistPath)!);
                using var f = File.Open(PersistPath, FileMode.Create); // Will override the current file
                await MessagePackSerializer.SerializeAsync<Flight[]>(f, flights, MessagePackOptions).ConfigureAwait(false);
            }
            finally
            {
                _persistSemaphore.Release();
            }
        }

        private async Task LoadFromPersistedData()
        {
            if (!File.Exists(PersistPath))
            {
                // File does not exist. Nothing to restore
                Console.WriteLine($"{GetType().Name} loaded no flights. The flight system starts empty.");
                return;
            }
            using var f = File.Open(PersistPath, FileMode.Open);
            var flights = await MessagePackSerializer.DeserializeAsync<Flight[]>(f, MessagePackOptions).ConfigureAwait(false);
            foreach (var flight in flights)
            {
                // This could be even faster if we called the C++ directly here with the entire array
                // But this is fine for the experiments where we don't use this
                await AddOrUpdateFlightAsync(flight).ConfigureAwait(false);
            }
            Console.WriteLine($"{GetType().Name} loaded {flights.Length} flights persisted on disk at {PersistPath}");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (_cudaFlightSystem is not null)
                {
                    _cudaFlightSystem.Dispose();
                }
                _persistSemaphore.Dispose();
                _persistTimer.Dispose();
                _persistBag.Clear();
                _flightIdentToIdMap.Clear();
                _flightIdToIdentMap.Clear();

                disposedValue = true;
            }
        }

        ~CUDAEventDataStore()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
