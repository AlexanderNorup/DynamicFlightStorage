using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection.Entities;
using Microsoft.EntityFrameworkCore;

namespace DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection
{
    public class ExperimentDataCollector
    {
        private SimulationEventBus _eventBus;
        private DataCollectionContext _context;
        public ExperimentDataCollector(SimulationEventBus eventBus, DataCollectionContext context)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task AddOrUpdateExperimentAsync(Experiment experiment)
        {
            ArgumentNullException.ThrowIfNull(experiment);
            if (await _context.Experiments.ContainsAsync(experiment))
            {
                _context.Experiments.Update(experiment);
            }
            else
            {
                _context.Experiments.Add(experiment);
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task AddOrUpdateExperimentResultAsync(ExperimentResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            _context.ExperimentResults.Update(result);

            await _context.SaveChangesAsync().ConfigureAwait(false);
        }

    }
}
