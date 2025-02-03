using DynamicFlightStorageDTOs;
using System.ComponentModel.DataAnnotations;

namespace SimplePostgreSQLDataStore.DbEntities
{
    public class FlightEntity
    {
        [Key]
        public required string FlightIdentification { get; set; }

        public ICollection<AirportEntity> Airports { get; } = new List<AirportEntity>();

        public DateTime ScheduledTimeOfDeparture { get; set; }

        public DateTime ScheduledTimeOfArrival { get; set; }

        public bool IsRecalculating { get; set; } = false;
    }
}
