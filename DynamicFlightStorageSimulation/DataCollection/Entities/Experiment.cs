using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection.Entities
{
    [Table("Experiment")]
    public class Experiment
    {
        [Key]
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public string? Name { get; set; }
        public DateTime SimulatedStartTime { get; init; }
        public DateTime SimulatedEndTime { get; init; }

        public DateTime SimulatedPreloadStartTime { get; init; }
        public DateTime SimulatedPreloadEndTime { get; init; }
        public bool PreloadAllFlights { get; init; }

        public required string DataSetName { get; init; }

        /// <summary>
        /// If value is 0 or negative, the simulation will run as fast as possible.
        /// </summary>
        public double TimeScale { get; init; } = 1.0;

        /// <summary>
        /// If enabled, will log all events when they're recieved by the Consumer to the database.
        /// </summary>
        public bool LoggingEnabled { get; init; } = false;

        /// <summary>
        /// If enabled, the recalculation-events will get back to the Consumer after they've been "processed".
        /// </summary>
        public bool DoRecalculationBounce { get; init; } = false;

        public ICollection<ExperimentResult> ExperimentResults { get; init; } = new List<ExperimentResult>();
    }
}
