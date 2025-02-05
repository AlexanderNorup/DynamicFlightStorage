using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection.Entities
{
    [Table("ExperimentResult")]
    public class ExperimentResult
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public required string ExperimentId { get; init; }
        public DateTime? UTCStartTime { get; set; }
        public DateTime? UTCEndTime { get; set; }
        public string? ExperimentError { get; set; }
        public bool ExperimentSuccess { get; set; }

        public List<ExperimentClientResult> ClientResults { get; set; } = new();

        public Experiment Experiment { get; init; } = null!;
    }
}
