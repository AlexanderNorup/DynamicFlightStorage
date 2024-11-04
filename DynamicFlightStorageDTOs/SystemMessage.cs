﻿using MessagePack;

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
        [Key(3)]
        public required SystemMessageType MessageType { get; set; }

        public enum SystemMessageType
        {
            Message = 0,
            LatencyRequest = 1,
            LatencyResponse = 2,
            NewExperiment = 3,
            NewExperimentReady = 4
        }
    }
}
