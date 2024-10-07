using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.Events;
using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Text.Json;

namespace DynamicFlightStorageSimulation
{
    public class SimulationEventBus : IDisposable
    {
        private readonly IMqttClient _mqttClient;
        private readonly EventBusConfig _eventBusConfig;
        private readonly MqttClientOptions _mqttClientOptions;
        public SimulationEventBus(EventBusConfig eventBusConfig)
        {
            _eventBusConfig = eventBusConfig ?? throw new ArgumentNullException(nameof(eventBusConfig));

            _mqttClientOptions = new MqttClientOptionsBuilder()
                .WithClientId($"{System.Net.Dns.GetHostName()}_{Guid.NewGuid()}")
                .WithTcpServer(_eventBusConfig.Host)
                .WithCredentials(_eventBusConfig.Username, _eventBusConfig.Password)
                .WithCleanSession()
                .Build();

            _mqttClient = new MqttFactory().CreateMqttClient();
        }

        public delegate void FlightStorageEventHandler(object sender, FlightStorageEvent e);
        public delegate void WeatherEventHandler(object sender, WeatherEvent e);
        public delegate void FlightRecalculationEventHandler(object sender, FlightRecalculationEvent e);

        public event FlightStorageEventHandler? OnFlightRecieved;
        public event WeatherEventHandler? OnWeatherRecieved;
        public event FlightRecalculationEventHandler? OnFlightRecalculationRecieved;

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

            _mqttClient.ApplicationMessageReceivedAsync += (e) =>
            {
                e.AutoAcknowledge = true;
                try
                {
                    if (e.ApplicationMessage.Topic == _eventBusConfig.FlightTopic)
                    {
                        var flight = JsonSerializer.Deserialize<Flight>(Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
                        OnFlightRecieved?.Invoke(this, new FlightStorageEvent(flight));
                    }
                    else if (e.ApplicationMessage.Topic == _eventBusConfig.WeatherTopic)
                    {
                        var weather = JsonSerializer.Deserialize<Weather>(Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
                        OnWeatherRecieved?.Invoke(this, new WeatherEvent(weather));
                    }
                    else if (e.ApplicationMessage.Topic == _eventBusConfig.RecalculationTopic)
                    {
                        var flight = JsonSerializer.Deserialize<Flight>(Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment));
                        OnFlightRecalculationRecieved?.Invoke(this, new FlightRecalculationEvent(flight));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception of type {ex.GetType().FullName} while processing message from {e.ApplicationMessage.Topic}");
                    // TODO: Do something here, because this should invalidate the experiment.
                }
                return Task.CompletedTask;
            };
        }

        public async Task DisconnectAsync()
        {
            await _mqttClient.DisconnectAsync();
        }

        public bool IsConnected() => _mqttClient.IsConnected;

        public void Dispose()
        {
            _mqttClient.Dispose();
        }
    }
}
