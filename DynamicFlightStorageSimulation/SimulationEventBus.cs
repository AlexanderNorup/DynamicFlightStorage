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
            if (flightStorageEventHandlers.Count == 0)
            {
                _mqttClient.SubscribeAsync(_eventBusConfig.FlightTopic).GetAwaiter().GetResult();
            }
            flightStorageEventHandlers.Add(handler);
        }

        public void SubscribeToWeatherEvent(WeatherEventHandler handler)
        {
            if (weatherEventHandlers.Count == 0)
            {
                _mqttClient.SubscribeAsync(_eventBusConfig.WeatherTopic).GetAwaiter().GetResult();
            }
            weatherEventHandlers.Add(handler);
        }

        public void SubscribeToRecalculationEvent(FlightRecalculationEventHandler handler)
        {
            if (flightRecalculationEventHandlers.Count == 0)
            {
                _mqttClient.SubscribeAsync(_eventBusConfig.RecalculationTopic).GetAwaiter().GetResult();
            }
            flightRecalculationEventHandlers.Add(handler);
        }

        public void SubscribeToSystemEvent(SystemMessageEventHandler handler)
        {
            systemMessageEventHandlers.Add(handler);
        }

        public void UnSubscribeToFlightStorageEvent(FlightStorageEventHandler handler)
        {
            flightStorageEventHandlers.Remove(handler);
            if (flightStorageEventHandlers.Count == 0)
            {
                _mqttClient.UnsubscribeAsync(_eventBusConfig.FlightTopic).GetAwaiter().GetResult();
            }
        }

        public void UnSubscribeToWeatherEvent(WeatherEventHandler handler)
        {
            weatherEventHandlers.Remove(handler);
            if (weatherEventHandlers.Count == 0)
            {
                _mqttClient.UnsubscribeAsync(_eventBusConfig.WeatherTopic).GetAwaiter().GetResult();
            }
        }

        public void UnSubscribeToRecalculationEvent(FlightRecalculationEventHandler handler)
        {
            flightRecalculationEventHandlers.Remove(handler);
            if (flightRecalculationEventHandlers.Count == 0)
            {
                _mqttClient.UnsubscribeAsync(_eventBusConfig.RecalculationTopic).GetAwaiter().GetResult();
            }
        }

        public void UnSubscribeToSystemEvent(SystemMessageEventHandler handler)
        {
            systemMessageEventHandlers.Remove(handler);
        }

        public Task PublishFlightAsync(params Flight[] flight)
        {
            return PublishMessageInternalAsync(_eventBusConfig.FlightTopic, MessagePackSerializer.Serialize(flight, _messagePackOptions));
        }

        public Task PublishWeatherAsync(params Weather[] weather)
        {
            return PublishMessageInternalAsync(_eventBusConfig.WeatherTopic, MessagePackSerializer.Serialize(weather, _messagePackOptions));
        }

        public Task PublishRecalculationAsync(params Flight[] flight)
        {
            return PublishMessageInternalAsync(_eventBusConfig.RecalculationTopic, MessagePackSerializer.Serialize(flight, _messagePackOptions));
        }

        public Task PublishSystemMessage(params SystemMessage[] systemMessage)
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

        public async Task ConnectAsync()
        {
            await _mqttClient.ConnectAsync(_mqttClientOptions);

            _logger?.LogInformation("Connected to MQTT {Host} as {ClientId}",
                _eventBusConfig.Host, _mqttClientOptions.ClientId);

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
                        var flights = MessagePackSerializer.Deserialize<Flight[]>(e.ApplicationMessage.PayloadSegment, _messagePackOptions);
                        if (flights is not null)
                        {
                            foreach (var handler in flightStorageEventHandlers)
                            {
                                foreach (var flight in flights)
                                {
                                    await handler(new FlightStorageEvent(flight)).ConfigureAwait(false);
                                }
                            }
                        }
                        e.IsHandled = true;
                    }
                    else if (e.ApplicationMessage.Topic == _eventBusConfig.WeatherTopic)
                    {
                        var weathers = MessagePackSerializer.Deserialize<Weather[]>(e.ApplicationMessage.PayloadSegment, _messagePackOptions);
                        if (weathers is not null)
                        {
                            foreach (var handler in weatherEventHandlers)
                            {
                                foreach (var weather in weathers)
                                {
                                    await handler(new WeatherEvent(weather)).ConfigureAwait(false);
                                }
                            }
                        }
                        e.IsHandled = true;
                    }
                    else if (e.ApplicationMessage.Topic == _eventBusConfig.RecalculationTopic)
                    {
                        var flights = MessagePackSerializer.Deserialize<Flight[]>(e.ApplicationMessage.PayloadSegment, _messagePackOptions);
                        if (flights is not null)
                        {
                            foreach (var handler in flightRecalculationEventHandlers)
                            {
                                foreach (var flight in flights)
                                {
                                    await handler(new FlightRecalculationEvent(flight)).ConfigureAwait(false);
                                }
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
                var systemMessages = MessagePackSerializer.Deserialize<SystemMessage[]>(e.ApplicationMessage.PayloadSegment, _messagePackOptions);
                if (systemMessages is not null)
                {
                    foreach (var handler in systemMessageEventHandlers)
                    {
                        foreach (var message in systemMessages)
                        {
                            _ = handler(new SystemMessageEvent(message));
                        }
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
