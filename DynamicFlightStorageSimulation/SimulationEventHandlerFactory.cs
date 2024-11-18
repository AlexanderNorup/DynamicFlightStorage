using MessagePack;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DynamicFlightStorageSimulation
{
    public static class SimulationEventHandlerFactory
    {
        public static AsyncEventHandler<BasicDeliverEventArgs> GetEventHandler<TMessage>(HashSet<Func<TMessage, Task>> messageHandlers,
            IChannel channel,
            MessagePackSerializerOptions messagePackOptions,
            ILogger? logger = null)
        {
            return async (sender, ea) =>
            {
                if (channel is null)
                {
                    return;
                }
                try
                {
                    var message = MessagePackSerializer.Deserialize<TMessage>(ea.Body.ToArray(), messagePackOptions);
                    var tasks = new List<Task>(messageHandlers.Count);
                    if (message is not null)
                    {
                        foreach (var handler in messageHandlers)
                        {
                            tasks.Add(handler(message));
                        }
                    }
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Exception of type {Name} while processing {Message} from {Exchange}",
                        ex.GetType().FullName,
                        typeof(TMessage).Name,
                        ea.Exchange);
                }
            };
        }
    }
}
