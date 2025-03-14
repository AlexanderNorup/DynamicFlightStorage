﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection.Entities
{
    [Table("EventLog_Flight")]
    public class FlightEventLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public required string ExperimentId { get; init; }

        public required string ClientId { get; init; }

        public required byte[] FlightData { get; init; }

        public required DateTime UtcTimeStamp { get; init; }
    }
}
