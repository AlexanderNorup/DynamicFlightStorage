using DynamicFlightStorageDTOs;
using DynamicFlightStorageSimulation.Events;
using MessagePack;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading.Channels;

namespace DynamicFlightStorageSimulation
{
    public class SimulationEventBus : IRecalculateFlightEventPublisher, IDisposable
    {
        private readonly ILogger<SimulationEventBus>? _logger;
        private IConnection? _rabbitConnection;
        private IChannel? _rabbitChannel;
        private readonly ConnectionFactory _rabbitConnectionFactory;
        private readonly EventBusConfig _eventBusConfig;

        private readonly MessagePackSerializerOptions _messagePackOptions;
        public SimulationEventBus(EventBusConfig eventBusConfig, ILogger<SimulationEventBus> logger)
        {
            _logger = logger;
            _eventBusConfig = eventBusConfig ?? throw new ArgumentNullException(nameof(eventBusConfig));

            _rabbitConnectionFactory = new ConnectionFactory()
            {
                HostName = _eventBusConfig.Host,
                UserName = _eventBusConfig.Username,
                Password = _eventBusConfig.Password,
                ClientProvidedName = $"{_eventBusConfig.FriendlyClientName ?? System.Net.Dns.GetHostName()}_{Guid.NewGuid()}",
            };

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

        public string ClientId => _rabbitConnection?.ClientProvidedName ?? string.Empty;

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

        private async Task PublishMessageInternalAsync(string exchange, byte[] payload)
        {
            if (!IsConnected() || _rabbitChannel is null)
            {
                throw new InvalidOperationException("Not connected to the event bus.");
            }

            await _rabbitChannel.BasicPublishAsync(exchange: exchange, string.Empty, payload);
        }

        public async Task ConnectAsync()
        {
            if (IsConnected())
            {
                return;
            }
            _rabbitConnection = await _rabbitConnectionFactory.CreateConnectionAsync();

            _logger?.LogInformation("Connected to RabbitMQ {Host} as {ClientId}",
                _eventBusConfig.Host, ClientId);

            _rabbitChannel = await _rabbitConnection.CreateChannelAsync();
            await _rabbitChannel.ExchangeDeclareAsync(_eventBusConfig.SystemTopic, ExchangeType.Fanout);
            var queueName = $"system_{ClientId}";
            await _rabbitChannel.QueueDeclareAsync(queue: queueName);
            await _rabbitChannel.QueueBindAsync(queue: queueName,
                exchange: _eventBusConfig.SystemTopic,
                routingKey: string.Empty);

            var systemConsumer = new AsyncEventingBasicConsumer(_rabbitChannel);
            systemConsumer.ReceivedAsync += HandleSystemMessage;
            await _rabbitChannel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: systemConsumer);

            //_rabbitConnection.ApplicationMessageReceivedAsync += async (e) =>
            //{
            //    e.AutoAcknowledge = true;
            //    try
            //    {
            //        if (e.ApplicationMessage.Topic == _eventBusConfig.FlightTopic)
            //        {
            //            var flights = MessagePackSerializer.Deserialize<Flight[]>(e.ApplicationMessage.PayloadSegment, _messagePackOptions);
            //            if (flights is not null)
            //            {
            //                foreach (var handler in flightStorageEventHandlers)
            //                {
            //                    foreach (var flight in flights)
            //                    {
            //                        await handler(new FlightStorageEvent(flight)).ConfigureAwait(false);
            //                    }
            //                }
            //            }
            //            e.IsHandled = true;
            //        }
            //        else if (e.ApplicationMessage.Topic == _eventBusConfig.WeatherTopic)
            //        {
            //            var weathers = MessagePackSerializer.Deserialize<Weather[]>(e.ApplicationMessage.PayloadSegment, _messagePackOptions);
            //            if (weathers is not null)
            //            {
            //                foreach (var handler in weatherEventHandlers)
            //                {
            //                    foreach (var weather in weathers)
            //                    {
            //                        await handler(new WeatherEvent(weather)).ConfigureAwait(false);
            //                    }
            //                }
            //            }
            //            e.IsHandled = true;
            //        }
            //        else if (e.ApplicationMessage.Topic == _eventBusConfig.RecalculationTopic)
            //        {
            //            var flights = MessagePackSerializer.Deserialize<Flight[]>(e.ApplicationMessage.PayloadSegment, _messagePackOptions);
            //            if (flights is not null)
            //            {
            //                foreach (var handler in flightRecalculationEventHandlers)
            //                {
            //                    foreach (var flight in flights)
            //                    {
            //                        await handler(new FlightRecalculationEvent(flight)).ConfigureAwait(false);
            //                    }
            //                }
            //            }
            //            e.IsHandled = true;
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        _logger?.LogError(ex, "Exception of type {Name} while processing message from {Topic}", ex.GetType().FullName, e.ApplicationMessage.Topic);
            //        // TODO: Do something here, because this should invalidate the experiment.
            //    }
            //};
        }

        private async Task HandleSystemMessage(object e, BasicDeliverEventArgs ea)
        {
            if (_rabbitChannel is null)
            {
                return;
            }
            try
            {
                var systemMessages = MessagePackSerializer.Deserialize<SystemMessage[]>(ea.Body, _messagePackOptions);
                var tasks = new List<Task>(systemMessageEventHandlers.Count * systemMessages.Length);
                if (systemMessages is not null)
                {
                    foreach (var handler in systemMessageEventHandlers)
                    {
                        foreach (var message in systemMessages)
                        {
                            tasks.Add(handler(new SystemMessageEvent(message)));
                        }
                    }
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
                await _rabbitChannel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception of type {Name} while processing system message from {Exchange}",
                    ex.GetType().FullName,
                    ea.Exchange);
            }
        }

        public async Task DisconnectAsync()
        {
            if (_rabbitConnection is null)
            {
                return;
            }
            _logger?.LogInformation("Disconnecting from RabbitMQ.");
            await _rabbitConnection.CloseAsync();
        }

        public bool IsConnected() => _rabbitConnection?.IsOpen ?? false;

        public void Dispose()
        {
            flightStorageEventHandlers.Clear();
            weatherEventHandlers.Clear();
            flightRecalculationEventHandlers.Clear();
            systemMessageEventHandlers.Clear();
            _rabbitChannel?.Dispose();
            _rabbitConnection?.Dispose();
        }
    }
}
