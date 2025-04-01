using DynamicFlightStorageDTOs;
using MessagePack;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

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

        private HashSet<Func<FlightEvent, Task>> flightStorageEventHandlers = new();
        private HashSet<Func<WeatherEvent, Task>> weatherEventHandlers = new();
        private HashSet<Func<FlightRecalculation, Task>> flightRecalculationEventHandlers = new();
        private HashSet<Func<SystemMessage, Task>> systemMessageEventHandlers = new();

        public string CurrentExperimentId { get; private set; } = string.Empty;
        public async Task SubscribeToExperiment(string experimentId)
        {
            if (string.IsNullOrWhiteSpace(experimentId))
            {
                throw new ArgumentNullException(nameof(experimentId));
            }
            if (experimentId == CurrentExperimentId || _rabbitChannel is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(CurrentExperimentId))
            {
                // We might already be bound to an experiment, so attempt to unbind first
                await TryUnbindQueueFromExchange(FlightQueueName, GetFlightExperimentExchange(experimentId));
                await TryUnbindQueueFromExchange(WeatherQueueName, GetWeatherExperimentExchange(experimentId));
            }

            CurrentExperimentId = experimentId;
            await _rabbitChannel.QueuePurgeAsync(FlightQueueName);
            await _rabbitChannel.QueueBindAsync(queue: FlightQueueName,
                exchange: GetFlightExperimentExchange(experimentId),
                routingKey: string.Empty);

            await _rabbitChannel.QueuePurgeAsync(WeatherQueueName);
            await _rabbitChannel.QueueBindAsync(queue: WeatherQueueName,
                exchange: GetWeatherExperimentExchange(experimentId),
                routingKey: string.Empty);
            _logger?.LogInformation("Bound to new experiment {ExperimentId}", experimentId);
        }

        public async Task UnSubscribeFromExperiment()
        {
            if (string.IsNullOrWhiteSpace(CurrentExperimentId) || _rabbitChannel is null)
            {
                return;
            }

            await TryUnbindQueueFromExchange(FlightQueueName, GetFlightExperimentExchange(CurrentExperimentId));
            await TryUnbindQueueFromExchange(WeatherQueueName, GetWeatherExperimentExchange(CurrentExperimentId));
            await _rabbitChannel.QueuePurgeAsync(FlightQueueName);
            await _rabbitChannel.QueuePurgeAsync(WeatherQueueName);
            _logger?.LogInformation("Unbound from experiment {ExperimentId}", CurrentExperimentId);
            CurrentExperimentId = string.Empty;
        }

        private async Task TryUnbindQueueFromExchange(string queue, string exchange)
        {
            if (_rabbitChannel is null)
            {
                return;
            }
            try
            {
                await _rabbitChannel.QueueUnbindAsync(queue: queue,
                    exchange: exchange,
                    routingKey: string.Empty);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error unbinding {Queue} from {Exchange} previous experiment {ExperimentId}", queue, exchange, CurrentExperimentId);
            }
        }

        public async Task CreateNewExperiment(string experimentId)
        {
            if (_rabbitChannel is null)
            {
                throw new InvalidOperationException("Not connected to the event bus");
            }

            await _rabbitChannel.ExchangeDeclareAsync(GetFlightExperimentExchange(experimentId), ExchangeType.Fanout, autoDelete: true);
            await _rabbitChannel.ExchangeDeclareAsync(GetWeatherExperimentExchange(experimentId), ExchangeType.Fanout, autoDelete: true);
        }

        public async Task DeleteExperimentExchanges(string experimentId)
        {
            if (_rabbitChannel is null)
            {
                throw new InvalidOperationException("Not connected to the event bus");
            }

            try
            {
                await _rabbitChannel.ExchangeDeleteAsync(GetFlightExperimentExchange(experimentId));
                await _rabbitChannel.ExchangeDeleteAsync(GetWeatherExperimentExchange(experimentId));
            }
            catch
            {
                // We don't actually care, exhcanged are auto-delete anyway, so they'll die eventually
            }
        }

        public string GetFlightExperimentExchange(string experimentId) => $"{_eventBusConfig.FlightTopic}.{experimentId}";
        public string GetWeatherExperimentExchange(string experimentId) => $"{_eventBusConfig.WeatherTopic}.{experimentId}";

        public string ClientId => _rabbitConnection?.ClientProvidedName ?? string.Empty;

        public const string FlightQueuePrefix = "flight_";
        public const string WeatherQueuePrefix = "weather_";
        public string FlightQueueName => $"{FlightQueuePrefix}{ClientId}";
        public string WeatherQueueName => $"{WeatherQueuePrefix}{ClientId}";
        public string SystemQueueName => $"system_{ClientId}";
        public string RecalculationQueueName => $"recalculation_{ClientId}";

        public void SubscribeToFlightStorageEvent(Func<FlightEvent, Task> handler)
        {
            flightStorageEventHandlers.Add(handler);
        }

        public void SubscribeToWeatherEvent(Func<WeatherEvent, Task> handler)
        {
            weatherEventHandlers.Add(handler);
        }

        public async Task SubscribeToRecalculationEventAsync(Func<FlightRecalculation, Task> handler)
        {
            await CreateAndBindToRecalculationAsync();
            flightRecalculationEventHandlers.Add(handler);
        }

        public void SubscribeToSystemEvent(Func<SystemMessage, Task> handler)
        {
            systemMessageEventHandlers.Add(handler);
        }

        public void UnSubscribeToFlightStorageEvent(Func<FlightEvent, Task> handler)
        {
            flightStorageEventHandlers.Remove(handler);
        }

        public void UnSubscribeToWeatherEvent(Func<WeatherEvent, Task> handler)
        {
            weatherEventHandlers.Remove(handler);
        }

        public void UnSubscribeToRecalculationEvent(Func<FlightRecalculation, Task> handler)
        {
            flightRecalculationEventHandlers.Remove(handler);
        }

        public void UnSubscribeToSystemEvent(Func<SystemMessage, Task> handler)
        {
            systemMessageEventHandlers.Remove(handler);
        }

        public Task PublishFlightAsync(Flight flight, string experimentId)
        {
            var flightEvent = new FlightEvent()
            {
                Flight = flight,
                TimeStamp = DateTime.UtcNow
            };
            return PublishMessageInternalAsync(GetFlightExperimentExchange(experimentId), MessagePackSerializer.Serialize(flightEvent, _messagePackOptions));
        }

        public Task PublishWeatherAsync(Weather weather, string experimentId)
        {
            var weatherEvent = new WeatherEvent()
            {
                Weather = weather,
                TimeStamp = DateTime.UtcNow
            };
            return PublishMessageInternalAsync(GetWeatherExperimentExchange(experimentId), MessagePackSerializer.Serialize(weatherEvent, _messagePackOptions));
        }

        public Task PublishWeatherServiceAsync(Dictionary<string, List<Weather>> weatherServiceData, string experimentId)
        {
            var weatherEvent = new WeatherEvent()
            {
                Weather = null,
                TimeStamp = DateTime.UtcNow,
                FullWeatherServiceData = weatherServiceData
            };
            return PublishMessageInternalAsync(GetWeatherExperimentExchange(experimentId), MessagePackSerializer.Serialize(weatherEvent, _messagePackOptions));
        }


        public Task PublishRecalculationAsync(string flightIdentification, string weatherIdentification, TimeSpan lag)
        {
            var recalculation = new FlightRecalculation()
            {
                FlightIdentification = flightIdentification,
                ExperimentId = CurrentExperimentId,
                ClientId = ClientId,
                RecalculatedTime = DateTime.UtcNow,
                TriggeredBy = weatherIdentification,
                LagInMilliseconds = lag.TotalMilliseconds,
            };
            return PublishMessageInternalAsync(_eventBusConfig.RecalculationTopic, MessagePackSerializer.Serialize(recalculation, _messagePackOptions));
        }

        public Task PublishSystemMessage(SystemMessage systemMessage)
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

        private SemaphoreSlim _connectionSemaphore = new SemaphoreSlim(1, 1);
        public async Task ConnectAsync()
        {
            if (IsConnected())
            {
                return;
            }
            await _connectionSemaphore.WaitAsync();
            try
            {
                if (IsConnected())
                {
                    return;
                }
                _rabbitConnection = await _rabbitConnectionFactory.CreateConnectionAsync();

                _logger?.LogInformation("Connected to RabbitMQ {Host} as {ClientId}",
                    _eventBusConfig.Host, ClientId);

                _rabbitChannel = await _rabbitConnection.CreateChannelAsync();
                await _rabbitChannel.BasicQosAsync(0, _eventBusConfig.MaxPrefetchPerConsumer, false);
                await _rabbitChannel.ExchangeDeclareAsync(_eventBusConfig.SystemTopic, ExchangeType.Fanout);
                await _rabbitChannel.ExchangeDeclareAsync(_eventBusConfig.RecalculationTopic, ExchangeType.Fanout);
                await RegisterQueueAndHandleAsync(systemMessageEventHandlers, SystemQueueName, exchangeToBind: _eventBusConfig.SystemTopic);
                await RegisterQueueAndHandleAsync(flightStorageEventHandlers, FlightQueueName);
                await RegisterQueueAndHandleAsync(weatherEventHandlers, WeatherQueueName);
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        private bool _recalculationEventCreated = false;
        private SemaphoreSlim _recaculationEventSemaphore = new SemaphoreSlim(1, 1);
        private async Task CreateAndBindToRecalculationAsync()
        {
            if (_recalculationEventCreated)
            {
                return;
            }
            await _recaculationEventSemaphore.WaitAsync();
            try
            {
                if (_recalculationEventCreated)
                {
                    return;
                }
                await RegisterQueueAndHandleAsync(flightRecalculationEventHandlers, RecalculationQueueName, exchangeToBind: _eventBusConfig.RecalculationTopic);
                _recalculationEventCreated = true;
            }
            finally
            {
                _recaculationEventSemaphore.Release();
            }
        }

        private async Task RegisterQueueAndHandleAsync<TMessage>(HashSet<Func<TMessage, Task>> handler, string queueName, string? exchangeToBind = null)
        {
            if (_rabbitChannel is null)
            {
                return;
            }
            await _rabbitChannel.QueueDeclareAsync(queue: queueName);
            if (exchangeToBind is { } ex)
            {
                await _rabbitChannel.QueueBindAsync(queue: queueName,
                    exchange: ex,
                    routingKey: string.Empty);
            }
            var consumer = new AsyncEventingBasicConsumer(_rabbitChannel);
            var eventHandler = SimulationEventHandlerFactory.GetEventHandler(
                handler,
                _rabbitChannel,
                _messagePackOptions,
                _logger);
            consumer.ReceivedAsync += eventHandler;
            await _rabbitChannel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);
        }

        public async Task ClearExchanges()
        {
            if (_rabbitChannel is null)
            {
                return;
            }

            _logger?.LogDebug("Bus is purging flight and weather queues");
            await _rabbitChannel.QueuePurgeAsync(FlightQueueName);
            await _rabbitChannel.QueuePurgeAsync(WeatherQueueName);
            _logger?.LogDebug("Bus has purged queues");
        }

        public async Task DisconnectAsync()
        {
            if (_rabbitConnection is null)
            {
                return;
            }
            _logger?.LogInformation("Disconnecting from RabbitMQ.");
            await _rabbitConnection.CloseAsync().ConfigureAwait(false);
        }

        public bool IsConnected() => _rabbitConnection?.IsOpen ?? false;

        public void Dispose()
        {
            flightStorageEventHandlers.Clear();
            weatherEventHandlers.Clear();
            flightRecalculationEventHandlers.Clear();
            systemMessageEventHandlers.Clear();
            _connectionSemaphore.Dispose();
            _recaculationEventSemaphore.Dispose();
            _rabbitChannel?.Dispose();
            _rabbitConnection?.Dispose();
        }
    }
}
