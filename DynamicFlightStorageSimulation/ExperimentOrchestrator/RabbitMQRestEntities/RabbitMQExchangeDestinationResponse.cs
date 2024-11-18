using System.Text.Json.Serialization;

namespace DynamicFlightStorageSimulation
{
    internal class RabbitMQExchangeDestinationResponse
    {
        [JsonPropertyName("destination")]
        public string Destination { get; set; }
    }
}
