using DynamicFlightStorageDTOs;

namespace OptimizedPostgreSQLDataStore
{
    public class BTreePostgrreSQLDataStore : BaseOptimizedPostgreSQLDataStore
    {
        public BTreePostgrreSQLDataStore(IWeatherService weatherService, IRecalculateFlightEventPublisher recalculateFlightEventPublisher)
            : base(weatherService, recalculateFlightEventPublisher, "btreeInit.sql")
        { }
    }
}