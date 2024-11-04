using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.Events;
using MessagePack;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace DynamicFlightStorageSimulation
{
    public class SimulationEventBus : IRecalculateFlightEventPublisher, IDisposable
    {
        private readonly ILogger<SimulationEventBus>? _logger;
        private readonly IMqttClient _mqttClient;
        private readonly EventBusConfig _eventBusConfig;
        private readonly MqttClientOptions _mqttClientOptions;
        private readonly MessagePackSerializerOptions _messagePackOptions;
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
            _messagePackOptions = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
        }

        public delegate Task FlightStorageEventHandler(FlightStorageEvent e);
        public delegate Task WeatherEventHandler(WeatherEvent e);
        public delegate Task FlightRecalculationEventHandler(FlightRecalculationEvent e);
        public delegate Task SystemMessageEventHandler(SystemMessageEvent e);

        private HashSet<FlightStorageEventHandler> flightStorageEventHandlers = new();
        private HashSet<WeatherEventHandler> weatherEventHandlers = new();
        private HashSet<FlightRecalculationEventHandler> flightRecalculationEventHandlers = new();
        private HashSet<SystemMessageEventHandler> systemMessageEventHandlers = new();

        public string ClientId => _mqttClient.Options.ClientId;

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

        public void SubscribeToSystemEvent(SystemMessageEventHandler handler)
        {
            systemMessageEventHandlers.Add(handler);
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

        public void UnSubscribeToSystemEvent(SystemMessageEventHandler handler)
        {
            systemMessageEventHandlers.Remove(handler);
        }

        public Task PublishFlightAsync(Flight flight)
        {
            return PublishMessageInternalAsync(_eventBusConfig.FlightTopic, MessagePackSerializer.Serialize(flight, _messagePackOptions));
        }

        public Task PublishWeatherAsync(Weather weather)
        {
            return PublishMessageInternalAsync(_eventBusConfig.WeatherTopic, MessagePackSerializer.Serialize(weather, _messagePackOptions));
        }

        public Task PublishRecalculationAsync(Flight flight)
        {
            return PublishMessageInternalAsync(_eventBusConfig.RecalculationTopic, MessagePackSerializer.Serialize(flight, _messagePackOptions));
        }

        public Task PublishSystemMessage(SystemMessage systemMessage)
        {
            return PublishMessageInternalAsync(_eventBusConfig.SystemTopic, MessagePackSerializer.Serialize(systemMessage, _messagePackOptions));
        }

        private async Task PublishMessageInternalAsync(string topic, byte[] payload)
        {
            if (!IsConnected())
            {
                throw new InvalidOperationException("Not connected to the event bus.");
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
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
            await _mqttClient.SubscribeAsync(_eventBusConfig.SystemTopic);
            // System messages requires it's own handler as we want to process them in parallel.
            _mqttClient.ApplicationMessageReceivedAsync += HandleSystemMessage;

            _mqttClient.ApplicationMessageReceivedAsync += async (e) =>
            {
                e.AutoAcknowledge = true;
                try
                {
                    if (e.ApplicationMessage.Topic == _eventBusConfig.FlightTopic)
                    {
                        var flight = MessagePackSerializer.Deserialize<Flight>(e.ApplicationMessage.PayloadSegment, _messagePackOptions);
                        if (flight is not null)
                        {
                            foreach (var handler in flightStorageEventHandlers)
                            {
                                await handler(new FlightStorageEvent(flight)).ConfigureAwait(false);
                            }
                        }
                        e.IsHandled = true;
                    }
                    else if (e.ApplicationMessage.Topic == _eventBusConfig.WeatherTopic)
                    {
                        var weather = MessagePackSerializer.Deserialize<Weather>(e.ApplicationMessage.PayloadSegment, _messagePackOptions);
                        if (weather is not null)
                        {
                            foreach (var handler in weatherEventHandlers)
                            {
                                await handler(new WeatherEvent(weather)).ConfigureAwait(false);
                            }
                        }
                        e.IsHandled = true;
                    }
                    else if (e.ApplicationMessage.Topic == _eventBusConfig.RecalculationTopic)
                    {
                        var flight = MessagePackSerializer.Deserialize<Flight>(e.ApplicationMessage.PayloadSegment, _messagePackOptions);
                        if (flight is not null)
                        {
                            foreach (var handler in flightRecalculationEventHandlers)
                            {
                                await handler(new FlightRecalculationEvent(flight)).ConfigureAwait(false);
                            }
                        }
                        e.IsHandled = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Exception of type {Name} while processing message from {Topic}", ex.GetType().FullName, e.ApplicationMessage.Topic);
                    // TODO: Do something here, because this should invalidate the experiment.
                }
            };
        }

        private Task HandleSystemMessage(MqttApplicationMessageReceivedEventArgs e)
        {
            if (!e.ApplicationMessage.Topic.Equals(_eventBusConfig.SystemTopic))
            {
                return Task.CompletedTask;
            }
            try
            {
                var systemMessage = MessagePackSerializer.Deserialize<SystemMessage>(e.ApplicationMessage.PayloadSegment, _messagePackOptions);
                if (systemMessage is not null)
                {
                    foreach (var handler in systemMessageEventHandlers)
                    {
                        _ = handler(new SystemMessageEvent(systemMessage));
                    }
                }
                e.IsHandled = true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception of type {Name} while processing system message from {Topic}", ex.GetType().FullName, e.ApplicationMessage.Topic);
            }
            return Task.CompletedTask;
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
            systemMessageEventHandlers.Clear();
            _mqttClient.Dispose();
        }
    }
}
