using System.Net.Http.Json;
using System.Text;
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

        public async Task WaitForExchangesToBeConsumedAsync(string experimentId, CancellationToken cancellationToken = default)
        {
            var allExchanges = await client.GetFromJsonAsync<RabbitMQExchangesResponse[]>("exchanges/%2F", cancellationToken).ConfigureAwait(false);
            var weatherExchangeForExperiment = allExchanges?.FirstOrDefault(e => e.Name == $"{config.WeatherTopic}.{experimentId}");
            var flightExchangeForExperiment = allExchanges?.FirstOrDefault(e => e.Name == $"{config.FlightTopic}.{experimentId}");

            if (weatherExchangeForExperiment is null || flightExchangeForExperiment is null)
            {
                throw new InvalidOperationException("Could not find exchanges for experiment");
            }

            var weatherQueue = await client.GetFromJsonAsync<RabbitMQExchangeDestinationResponse[]>($"exchanges/%2F/{weatherExchangeForExperiment.Name}/bindings/source", cancellationToken).ConfigureAwait(false);
            var flightQueue = await client.GetFromJsonAsync<RabbitMQExchangeDestinationResponse[]>($"exchanges/%2F/{flightExchangeForExperiment.Name}/bindings/source", cancellationToken).ConfigureAwait(false);
            var flightQueueId = flightQueue!.First().Destination;
            var weatherQueueId = weatherQueue!.First().Destination;

            while (!cancellationToken.IsCancellationRequested)
            {
                var flightResponse = await client.GetFromJsonAsync<RabbitMQQueueMetricsResponse>($"queues/%2F/{flightQueueId}", cancellationToken).ConfigureAwait(false);
                var weatherResponse = await client.GetFromJsonAsync<RabbitMQQueueMetricsResponse>($"queues/%2F/{weatherQueueId}", cancellationToken).ConfigureAwait(false);
                if (weatherResponse?.TotalMessages == 0 && flightResponse?.TotalMessages == 0)
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
