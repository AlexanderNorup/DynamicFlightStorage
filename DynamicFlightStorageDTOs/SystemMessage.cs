using MessagePack;

namespace DynamicFlightStorageDTOs
{
    [MessagePackObject]
    public class SystemMessage
    {
        [Key(0)]
        public required string Message { get; set; }
        [Key(1)]
        public required string Source { get; set; }
        [Key(2)]
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// If empty, this message is for all clients.
        /// </summary>
        [Key(3)]
        public HashSet<string> Targets { get; set; } = new();
        [Key(4)]
        public required SystemMessageType MessageType { get; set; }
        [Key(5)]
        public Dictionary<string, object>? Data { get; set; }

        public enum SystemMessageType
        {
            Message = 0,
            LatencyRequest = 1,
            LatencyResponse = 2,
            NewExperiment = 3,
            NewExperimentReady = 4,
            ExperimentComplete = 5,
            AbortExperiment = 6,
            AbortSuccess = 7,
            ConsumerExperimentAbort = 8,
            ExperimentPreloadDone = 9,
        }

        public override string ToString()
        {
            var targets = Targets.Count > 0 ? string.Join(", ", Targets) : "All Clients";
            var data = Data != null ? string.Join(", ", Data.Select(kv => $"{kv.Key}: {kv.Value}")) : "None";

            return $"[{TimeStamp:O}] {MessageType} from {Source}: \"{Message}\" " +
                   $"(Targets: {targets}, Data: {data})";
        }
    }
}
