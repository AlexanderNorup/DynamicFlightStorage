using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator
{
    public class ExperimentResult
    {
        public required Experiment Experiment { get; init; }
        public DateTime ExperimentStarted { get; set; }
        public DateTime ExperimentEnded { get; set; }
        public List<LatencyTestResult> LatencyTestResults { get; set; } = new();
        public string? ExperimentError { get; set; }
        public bool ExperimentSuccess { get; set; }
    }
}
