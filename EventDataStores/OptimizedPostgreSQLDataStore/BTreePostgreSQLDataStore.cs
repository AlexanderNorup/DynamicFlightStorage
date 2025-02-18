using DynamicFlightStorageDTOs;

namespace OptimizedPostgreSQLDataStore
{
    public class BTreePostgreSQLDataStore : BaseOptimizedPostgreSQLDataStore
    {
        public BTreePostgreSQLDataStore(IWeatherService weatherService, IRecalculateFlightEventPublisher recalculateFlightEventPublisher)
            : base(weatherService, recalculateFlightEventPublisher, "btreeInit.sql")
        { }
    }
}