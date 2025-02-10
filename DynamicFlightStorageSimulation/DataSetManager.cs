namespace DynamicFlightStorageSimulation
{
    public class DataSetManager
    {
        private const string FlightDirectory = "flightfiles";
        private const string WeatherDirectory = "weatherfiles";
        private const string WeatherMetar = "metar";
        private const string WeatherTaf = "taf";
        private readonly string _dataSetsPath;
        private readonly SimulationEventBus _eventBus;
        public DataSetManager(string dataSetsPath, SimulationEventBus eventBus)
        {
            _dataSetsPath = dataSetsPath ?? throw new ArgumentNullException(nameof(dataSetsPath));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public List<string> GetAvailableDatasets()
        {
            var dataSets = new List<string>();
            foreach (var dataSetName in Directory.GetDirectories(_dataSetsPath)
                .Select(Path.GetFileName)
                .OfType<string>()) // Filters away null
            {
                if (Directory.Exists(Path.Combine(_dataSetsPath, dataSetName, FlightDirectory))
                    && Directory.Exists(Path.Combine(_dataSetsPath, dataSetName, WeatherDirectory, WeatherMetar))
                    && Directory.Exists(Path.Combine(_dataSetsPath, dataSetName, WeatherDirectory, WeatherTaf)))
                {
                    dataSets.Add(dataSetName);
                }
            }
            return dataSets;
        }

        public (FlightInjector FlightInjector, WeatherInjector WeatherInjector) GetInjectorsForDataSet(string dataSetName)
        {
            if (string.IsNullOrWhiteSpace(dataSetName))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(dataSetName));
            }

            var dataSetPath = Path.Combine(_dataSetsPath, dataSetName);
            if (!Directory.Exists(dataSetPath))
            {
                throw new DirectoryNotFoundException($"The dataset '{dataSetName}' was not found.");
            }

            var flightFolder = Path.Combine(dataSetPath, FlightDirectory);
            if (!Directory.Exists(flightFolder))
            {
                throw new DirectoryNotFoundException($"The dataset '{dataSetName}' contained no flights.");
            }

            var metarFolder = Path.Combine(dataSetPath, WeatherDirectory, WeatherMetar);
            if (!Directory.Exists(metarFolder))
            {
                throw new DirectoryNotFoundException($"The dataset '{dataSetName}' contained no weather metars.");
            }

            var tafFolder = Path.Combine(dataSetPath, WeatherDirectory, WeatherTaf);
            if (!Directory.Exists(tafFolder))
            {
                throw new DirectoryNotFoundException($"The dataset '{dataSetName}' contained no weather tafs.");
            }

            var flight = new FlightInjector(_eventBus, flightFolder);
            var weather = new WeatherInjector(_eventBus, metarFolder, tafFolder);
            return (flight, weather);
        }
    }
}
