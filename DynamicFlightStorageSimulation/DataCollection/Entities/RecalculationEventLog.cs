using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection.Entities
{
    [Table("EventLog_Recalculation")]
    public class RecalculationEventLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public required string ExperimentId { get; init; }

        public required string ClientId { get; init; }

        public required string FlightId { get; init; }

        public required DateTime UtcTimeStamp { get; init; }
    }
}
