using DynamicFlightStorageDTOs;

namespace DynamicFlightStorageSimulation.Events
{
    public class SystemMessageEvent
    {
        public SystemMessageEvent(SystemMessage systemMessage)
        {
            SystemMessage = systemMessage ?? throw new ArgumentNullException(nameof(systemMessage));
        }

        public SystemMessage SystemMessage { get; }
    }
}
