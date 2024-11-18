using System.Net.Http.Json;
using System.Text;
using System.Threading;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.RabbitMQRestEntities;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator
{
    public class ConsumingMonitor
    {
        private readonly EventBusConfig config;
        private readonly HttpClient client;

        public ConsumingMonitor(EventBusConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            if (config.ApiHost is null)
            {
                throw new InvalidOperationException("ApiHost is null in Config. Cannot get queue metrics");
            }
            var authenticationString = $"{config.Username}:{config.Password}";
            var base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.UTF8.GetBytes(authenticationString));
            client = new HttpClient()
            {
                BaseAddress = GetBaseAddress()
            };
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + base64EncodedAuthenticationString);
        }

        public async Task<Dictionary<string, (int flightLag, int weatherLag)>> GetMessageLagAsync(string[] clientids, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<string, (int flightLag, int weatherLag)>(clientids.Length * 2);
            var queues = await client.GetFromJsonAsync<RabbitMQQueueMetricsResponse[]>($"queues/%2F/", cancellationToken).ConfigureAwait(false);
            foreach (var clientId in clientids)
            {
                var flightLag = -1;
                var weatherLag = -1;
                foreach (var queue in queues!)
                {
                    if (queue.Name == SimulationEventBus.FlightQueuePrefix + clientId)
                    {
                        flightLag = queue.TotalMessages;
                    }
                    else if (queue.Name == SimulationEventBus.WeatherQueuePrefix + clientId)
                    {
                        weatherLag = queue.TotalMessages;
                    }
                }

                result.Add(clientId, (flightLag, weatherLag));
            }

            return result;
        }

        public async Task WaitForExchangesToBeConsumedAsync(string[] clientIds, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var lag = await GetMessageLagAsync(clientIds, cancellationToken);
                if (lag.Values.All(x => x.weatherLag == 0 && x.flightLag == 0))
                {
                    break;
                }
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
        }

        private Uri GetBaseAddress()
        {
            return new Uri($"https://{config.ApiHost}/api/");
        }
    }
}
