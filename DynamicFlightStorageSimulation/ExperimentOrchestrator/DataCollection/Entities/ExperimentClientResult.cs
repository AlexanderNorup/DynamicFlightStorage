using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection.Entities
{
    [Table("ExperimentClientResult")]
    public class ExperimentClientResult
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey("ExperimentResultId")]
        public ExperimentResult ExperimentResult { get; set; }
        public required string ClientId { get; set; }
        [ForeignKey("LatencyTestId")]
        public LatencyTestResult? LatencyTest { get; set; }
        public int MaxFlightConsumerLag { get; set; } = -1;
        public int MaxWeatherConsumerLag { get; set; } = -1;
    }
}