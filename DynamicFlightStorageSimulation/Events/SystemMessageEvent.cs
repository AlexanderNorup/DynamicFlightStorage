using DynamicFlightStorageDTOs;
using MessagePack;

namespace DynamicFlightStorageSimulation.Events
{
    [MessagePackObject]
    public class SystemMessageEvent
    {
        public SystemMessageEvent(SystemMessage systemMessage)
        {
            SystemMessage = systemMessage ?? throw new ArgumentNullException(nameof(systemMessage));
        }

        [Key(0)]
        public SystemMessage SystemMessage { get; }
    }
}
