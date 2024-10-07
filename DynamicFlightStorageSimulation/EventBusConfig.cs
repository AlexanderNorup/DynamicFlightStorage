namespace DynamicFlightStorageSimulation
{
    public class EventBusConfig
    {
        public required string Host { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string FlightTopic { get; set; }
        public required string WeatherTopic { get; set; }
        public required string RecalculationTopic { get; set; }
    }
}
