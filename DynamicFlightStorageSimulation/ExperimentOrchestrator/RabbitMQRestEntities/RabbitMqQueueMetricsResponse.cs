using System.Text.Json.Serialization;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator.RabbitMQRestEntities
{
    internal class RabbitMQQueueMetricsResponse
    {
        [JsonPropertyName("idle_since")]
        public DateTime IdleSince { get; set; }

        [JsonPropertyName("messages")]
        public int TotalMessages { get; set; }

        [JsonPropertyName("messages_ready")]
        public int MessagesReady { get; set; }

        [JsonPropertyName("messages_unacknowledged")]
        public int MessagesUnacknowledged { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
