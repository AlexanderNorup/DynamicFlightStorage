using DynamicFlightStorageDTOs;


namespace GPUAcceleratedEventDataStore
{
    public class CUDAEventDataStore : IEventDataStore, IDisposable
    {
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
                int newId = CreateNewFlightId(flight.FlightIdentification);
                _cudaFlightSystem.AddFlights(new GPUFlight(flight, newId, weather));
            }
            return Task.CompletedTask;
        }

        public async Task AddWeatherAsync(Weather weather)
        {
            if (_cudaFlightSystem is null)
            {
                throw new InvalidOperationException("CUDA Flight System is not ready");
            }

            int[] affectedFlights = _cudaFlightSystem.FindFlightsAffectedByWeather(weather);
            foreach (var flight in affectedFlights)
            {
                if (_flightIdToIdentMap.TryGetValue(flight, out var ident))
                {
                    await _flightRecalculation.PublishRecalculationAsync(ident);
                }
                else
                {
                    Console.WriteLine($"Trying to recalculate flight with id {flight}, but a corrosponding flight identification was not found");
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
            if (_cudaFlightSystem is not null)
            {
                _cudaFlightSystem.Dispose();
            }
            _flightIdentToIdMap.Clear();
            _flightIdToIdentMap.Clear();
            _nextFlightId = 0;
            return StartAsync();
        }

        public Task StartAsync()
        {
            _cudaFlightSystem = new CudaFlightSystem();
            _cudaFlightSystem.Initialize();
            return Task.CompletedTask;
        }

        private int CreateNewFlightId(string flightIdentification)
        {
            int id = _nextFlightId++;
            _flightIdToIdentMap.Add(id, flightIdentification);
            _flightIdentToIdMap.Add(flightIdentification, id);

            return id;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (_cudaFlightSystem is not null)
                {
                    _cudaFlightSystem.Dispose();
                }

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
