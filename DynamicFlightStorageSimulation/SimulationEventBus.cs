using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.Events;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Text.Json;

namespace DynamicFlightStorageSimulation
{
    public class SimulationEventBus : IRecalculateFlightEventPublisher, IDisposable
    {
        private readonly ILogger<SimulationEventBus>? _logger;
        private readonly IMqttClient _mqttClient;
        private readonly EventBusConfig _eventBusConfig;
        private readonly MqttClientOptions _mqttClientOptions;
        public SimulationEventBus(EventBusConfig eventBusConfig, ILogger<SimulationEventBus> logger)
        {
            _logger = logger;
            _eventBusConfig = eventBusConfig ?? throw new ArgumentNullException(nameof(eventBusConfig));

            _mqttClientOptions = new MqttClientOptionsBuilder()
                .WithClientId($"{System.Net.Dns.GetHostName()}_{Guid.NewGuid()}")
                .WithTcpServer(_eventBusConfig.Host)
                .WithCredentials(_eventBusConfig.Username, _eventBusConfig.Password)
                .WithCleanSession()
                .Build();

            _mqttClient = new MqttFactory().CreateMqttClient();
        }

        public delegate Task FlightStorageEventHandler(FlightStorageEvent e);
        public delegate Task WeatherEventHandler(WeatherEvent e);
        public delegate Task FlightRecalculationEventHandler(FlightRecalculationEvent e);

        private HashSet<FlightStorageEventHandler> flightStorageEventHandlers = new();
        private HashSet<WeatherEventHandler> weatherEventHandlers = new();
        private HashSet<FlightRecalculationEventHandler> flightRecalculationEventHandlers = new();

        public void SubscribeToFlightStorageEvent(FlightStorageEventHandler handler)
        {
            flightStorageEventHandlers.Add(handler);
        }

        public void SubscribeToWeatherEvent(WeatherEventHandler handler)
        {
            weatherEventHandlers.Add(handler);
        }

        public void SubscribeToRecalculationEvent(FlightRecalculationEventHandler handler)
        {
            flightRecalculationEventHandlers.Add(handler);
        }

        public void UnSubscribeToFlightStorageEvent(FlightStorageEventHandler handler)
        {
            flightStorageEventHandlers.Remove(handler);
        }

        public void UnSubscribeToWeatherEvent(WeatherEventHandler handler)
        {
            weatherEventHandlers.Remove(handler);
        }

        public void UnSubscribeToRecalculationEvent(FlightRecalculationEventHandler handler)
        {
            flightRecalculationEventHandlers.Remove(handler);
        }

        public Task PublishFlightAsync(Flight flight)
        {
            return PublishMessageInternalAsync(_eventBusConfig.FlightTopic, JsonSerializer.Serialize(flight));
        }

        public Task PublishWeatherAsync(Weather weather)
        {
            return PublishMessageInternalAsync(_eventBusConfig.WeatherTopic, JsonSerializer.Serialize(weather));
        }

        public Task PublishRecalculationAsync(Flight flight)
        {
            return PublishMessageInternalAsync(_eventBusConfig.RecalculationTopic, JsonSerializer.Serialize(flight));
        }

        private async Task PublishMessageInternalAsync(string topic, string payload)
        {
            if (!IsConnected())
            {
                throw new InvalidOperationException("Not connected to the event bus.");
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build();

            await _mqttClient.PublishAsync(message).ConfigureAwait(false);
        }

        public async Task ConnectAsync(bool withFlightTopic, bool withWeatherTopic, bool withRecalculationTopic)
        {
            await _mqttClient.ConnectAsync(_mqttClientOptions);

            _logger?.LogInformation("Connected to MQTT {Host} as {ClientId}.\n" +
                "Subscribing to Flight:{Flight}, Weather:{Weather}, Recalculation:{Recalculation}",
                _eventBusConfig.Host, _mqttClientOptions.ClientId, withFlightTopic, withWeatherTopic, withRecalculationTopic);

            if (withFlightTopic)
            {
                await _mqttClient.SubscribeAsync(_eventBusConfig.FlightTopic);
            }
            if (withWeatherTopic)
            {
                await _mqttClient.SubscribeAsync(_eventBusConfig.WeatherTopic);
            }
            if (withRecalculationTopic)
            {
                await _mqttClient.SubscribeAsync(_eventBusConfig.RecalculationTopic);
            }

            _mqttClient.ApplicationMessageReceivedAsync += async (e) =>
            {
                e.AutoAcknowledge = true;
                try
                {
                    if (e.ApplicationMessage.Topic == _eventBusConfig.FlightTopic)
                    {
                        var flight = JsonSerializer.Deserialize<Flight>(Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
                        if (flight is not null)
                        {
                            foreach (var handler in flightStorageEventHandlers)
                            {
                                await handler(new FlightStorageEvent(flight)).ConfigureAwait(false);
                            }
                        }
                    }
                    else if (e.ApplicationMessage.Topic == _eventBusConfig.WeatherTopic)
                    {
                        var weather = JsonSerializer.Deserialize<Weather>(Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
                        if (weather is not null)
                        {
                            foreach (var handler in weatherEventHandlers)
                            {
                                await handler(new WeatherEvent(weather)).ConfigureAwait(false);
                            }
                        }
                    }
                    else if (e.ApplicationMessage.Topic == _eventBusConfig.RecalculationTopic)
                    {
                        var flight = JsonSerializer.Deserialize<Flight>(Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
                        if (flight is not null)
                        {
                            foreach (var handler in flightRecalculationEventHandlers)
                            {
                                await handler(new FlightRecalculationEvent(flight)).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Exception of type {Name} while processing message from {Topic}", ex.GetType().FullName, e.ApplicationMessage.Topic);
                    // TODO: Do something here, because this should invalidate the experiment.
                }
            };
        }

        public async Task DisconnectAsync()
        {
            _logger?.LogInformation("Disconnecting from the event bus.");
            await _mqttClient.DisconnectAsync();
        }

        public bool IsConnected() => _mqttClient.IsConnected;

        public void Dispose()
        {
            flightStorageEventHandlers.Clear();
            weatherEventHandlers.Clear();
            flightRecalculationEventHandlers.Clear();
            _mqttClient.Dispose();
        }
    }
}
