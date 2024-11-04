
using DynamicFlightStorageSimulation;

namespace DynamicFlightStorageUI
{
    public class EventBusConnector(SimulationEventBus _simulationEventBus) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _simulationEventBus.ConnectAsync();
        }
    }
}
