using System.Text.Json.Serialization;

namespace DynamicFlightStorageSimulation
{
    internal class RabbitMQExchangesResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
