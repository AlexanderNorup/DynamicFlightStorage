﻿using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection;
using MessagePack;

namespace DynamicFlightStorageSimulation.DataCollection
{
    public class ConsumerDataLogger
    {
        private readonly DataCollectionContext _dbContext;
        public static readonly MessagePackSerializerOptions MessagePackOptions = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
        public ConsumerDataLogger(DataCollectionContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public bool IsLoggingEnabled { get; set; } = false;

        private LinkedList<FlightLog> _flightLog = new();
        private LinkedList<WeatherLog> _weatherLog = new();
        public void LogFlightData(FlightEvent flight)
        {
            if (IsLoggingEnabled)
            {
                _flightLog.AddLast(new FlightLog(DateTime.UtcNow, flight.TimeStamp, flight.Flight));
            }
        }

        public void LogWeatherData(WeatherEvent weather)
        {
            if (IsLoggingEnabled)
            {
                _weatherLog.AddLast(new WeatherLog(DateTime.UtcNow, weather.TimeStamp, weather.Weather));
            }
        }

        public async Task PersistDataAsync(string experimentId)
        {
            if (!IsLoggingEnabled)
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(experimentId, nameof(experimentId));

            var flightData = MessagePackSerializer.Serialize(_flightLog, MessagePackOptions);
            var weatherData = MessagePackSerializer.Serialize(_weatherLog, MessagePackOptions);

            var outputPath = Path.Combine(AppContext.BaseDirectory, "experiment_logs", experimentId);
            Directory.CreateDirectory(outputPath);
            await File.WriteAllBytesAsync(Path.Combine(outputPath, "flights.bin"), flightData);
            await File.WriteAllBytesAsync(Path.Combine(outputPath, "weather.bin"), weatherData);

            _dbContext.WeatherEventLogs.Add(new ExperimentOrchestrator.DataCollection.Entities.WeatherEventLog()
            {
                ExperimentId = experimentId,
                WeatherData = weatherData,
                UtcTimeStamp = DateTime.UtcNow
            });
            _dbContext.FlightEventLogs.Add(new ExperimentOrchestrator.DataCollection.Entities.FlightEventLog()
            {
                ExperimentId = experimentId,
                FlightData = flightData,
                UtcTimeStamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync().ConfigureAwait(false);
        }

        public void ResetLogger()
        {
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
            [property: Key(2)] Weather Flight);
    }
}
