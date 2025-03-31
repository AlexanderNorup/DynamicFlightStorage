using System.Text.Json.Serialization;

namespace DynamicFlightStorageUI
{
    public class PushoverNotification
    {
        [JsonPropertyName("title")]
        public required string Title { get; init; }
        [JsonPropertyName("message")]
        public required string Message { get; init; }
        [JsonPropertyName("user")]
        public required string User { get; init; }
        [JsonPropertyName("token")]
        public required string Token { get; init; }
    }
}
