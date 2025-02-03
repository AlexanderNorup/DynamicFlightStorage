using DynamicFlightStorageDTOs;
using System.ComponentModel.DataAnnotations;

namespace SimplePostgreSQLDataStore.DbEntities
{
    public class AirportEntity
    {
        [Key]
        public int Id { get; set; }

        public string FlightEntityId { get; set; } = null!;

        public FlightEntity FlightEntity { get; set; } = null!;

        public required string ICAO { get; set; }

        public WeatherCategory LastSeenWeatherCategory { get; set; } = WeatherCategory.Undefined;
    }
}