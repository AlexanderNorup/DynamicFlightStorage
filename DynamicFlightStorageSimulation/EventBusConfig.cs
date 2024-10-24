using System.ComponentModel.DataAnnotations;

namespace DynamicFlightStorageSimulation
{
    public class EventBusConfig
    {
        [Required]
        public required string Host { get; set; }

        [Required]
        public required string Username { get; set; }

        [Required]
        public required string Password { get; set; }

        [Required]
        public required string FlightTopic { get; set; }

        [Required]
        public required string WeatherTopic { get; set; }

        [Required]
        public required string RecalculationTopic { get; set; }

        public static EventBusConfig GetEmpty()
        {
            return new EventBusConfig()
            {
                Host = string.Empty,
                Username = string.Empty,
                Password = string.Empty,
                FlightTopic = string.Empty,
                WeatherTopic = string.Empty,
                RecalculationTopic = string.Empty
            };
        }
    }
}
