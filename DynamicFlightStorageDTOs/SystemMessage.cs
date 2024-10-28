namespace DynamicFlightStorageDTOs
{
    public class SystemMessage
    {
        public required string Message { get; set; }
        public required string Source { get; set; }
        public required DateTime TimeStamp { get; set; }
        public required SystemMessageType MessageType { get; set; }

        public enum SystemMessageType
        {
            Message = 0,
            LatencyRequest = 1,
            LatencyResponse = 2
        }
    }
}
