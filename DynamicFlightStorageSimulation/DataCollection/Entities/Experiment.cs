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

        /// <summary>
        /// If value is 0 or negative, the simulation will run as fast as possible.
        /// </summary>
        public double TimeScale { get; init; } = 1.0;

        /// <summary>
        /// If enabled, will log all events when they're recieved by the Consumer to the database.
        /// </summary>
        public bool LoggingEnabled { get; init; } = false;
    }
}
