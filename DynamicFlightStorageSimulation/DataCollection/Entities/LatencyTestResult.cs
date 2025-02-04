using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection.Entities
{
    [Table("LatencyTest")]
    public class LatencyTestResult
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [NotMapped]
        public required string ClientId { get; set; }
        [NotMapped]
        public bool Success { get; set; }
        public int SamplePoints { get; set; }
        public int SampleDelayMs { get; set; }
        public double AverageLatencyMs { get; set; }
        public double MedianLatencyMs { get; set; }
        public double StdDeviationLatency { get; set; }
    }
}
