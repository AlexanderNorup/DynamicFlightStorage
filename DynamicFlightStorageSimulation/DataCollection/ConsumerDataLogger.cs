using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO.Compression;

namespace DynamicFlightStorageSimulation.DataCollection
{
    public class ConsumerDataLogger
    {
        private readonly DbContextOptions<DataCollectionContext> _dbContextOptions;
        private readonly ILogger<ConsumerDataLogger> _logger;
        public static readonly MessagePackSerializerOptions MessagePackOptions = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

        public ConsumerDataLogger(DbContextOptions<DataCollectionContext> dbContextOptions, ILogger<ConsumerDataLogger> logger)
        {
            _dbContextOptions = dbContextOptions ?? throw new ArgumentNullException(nameof(dbContextOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsLoggingEnabled { get; set; } = false;
        public bool IsPreloadDone { get; set; } = false;
        public bool ShouldLog => IsLoggingEnabled && IsPreloadDone;

        private LinkedList<FlightLog> _flightLog = new();
        private LinkedList<WeatherLog> _weatherLog = new();
        public void LogFlightData(FlightEvent flight)
        {
            if (ShouldLog)
            {
                _flightLog.AddLast(new FlightLog(DateTime.UtcNow, flight.TimeStamp, flight.Flight));
            }
        }

        public void LogWeatherData(WeatherEvent weather)
        {
            if (ShouldLog)
            {
                _weatherLog.AddLast(new WeatherLog(DateTime.UtcNow, weather.TimeStamp, weather.Weather));
            }
        }

        public async Task PersistDataAsync(string experimentId, string clientId)
        {
            if (!IsLoggingEnabled)
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(experimentId, nameof(experimentId));
            ArgumentNullException.ThrowIfNull(clientId, nameof(clientId));

            _logger.LogInformation("Starting to persist data for experiment {ExperimentId} and client {ClientId}. Flight count={FlightCount}, Weather count={WeatherCount}",
                experimentId, clientId, _flightLog.Count, _weatherLog.Count);

            var flightData = MessagePackSerializer.Serialize(_flightLog, MessagePackOptions);
            var weatherData = MessagePackSerializer.Serialize(_weatherLog, MessagePackOptions);

            var outputPath = Path.Combine(AppContext.BaseDirectory, "experiment_logs", experimentId, clientId);
            Directory.CreateDirectory(outputPath);
            await File.WriteAllBytesAsync(Path.Combine(outputPath, "flights.bin"), flightData);
            await File.WriteAllBytesAsync(Path.Combine(outputPath, "weather.bin"), weatherData);

            _logger.LogInformation("Successfully wrote data to disk for experiment {ExperimentId} and client {ClientId} => {Path}",
                experimentId, clientId, outputPath);

            // Compress data for the database
            var compressedFlights = CompressionHelpers.Compress(flightData);
            var compressedWeather = CompressionHelpers.Compress(weatherData);
            const double BytesPerKilobyte = 1024;

            _logger.LogInformation("Successfully compressed data for experiment {ExperimentId} and client {ClientId}. " +
                "Saved {SavedFlightBytes} kb for flights and {SavedWeatherBytes} kb for weather",
                experimentId, clientId,
                (flightData.LongLength - compressedFlights.LongLength) / BytesPerKilobyte,
                (weatherData.LongLength - compressedWeather.LongLength) / BytesPerKilobyte);

            using var dbContext = new DataCollectionContext(_dbContextOptions);

            dbContext.WeatherEventLogs.Add(new ExperimentOrchestrator.DataCollection.Entities.WeatherEventLog()
            {
                ExperimentId = experimentId,
                ClientId = clientId,
                WeatherData = compressedWeather,
                UtcTimeStamp = DateTime.UtcNow
            });
            dbContext.FlightEventLogs.Add(new ExperimentOrchestrator.DataCollection.Entities.FlightEventLog()
            {
                ExperimentId = experimentId,
                ClientId = clientId,
                FlightData = compressedFlights,
                UtcTimeStamp = DateTime.UtcNow
            });

            _logger.LogInformation("Uploading data to database for experiment {ExperimentId} and client {ClientId}", experimentId, clientId);
            try
            {
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload data to database for experiment {ExperimentId} and client {ClientId}", experimentId, clientId);
            }
        }

        public void ResetLogger()
        {
            IsPreloadDone = false;
            _flightLog.Clear();
            _weatherLog.Clear();
        }

        [MessagePackObject]
        public record FlightLog([property: Key(0)] DateTime ReceivedTimestamp,
            [property: Key(1)] DateTime SentTimestamp,
            [property: Key(2)] Flight Flight);

        [MessagePackObject]
        public record WeatherLog([property: Key(0)] DateTime ReceivedTimestamp,
            [property: Key(1)] DateTime SentTimestamp,
            [property: Key(2)] Weather Weather);
    }
}
