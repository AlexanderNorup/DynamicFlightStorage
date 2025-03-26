using DynamicFlightStorageDTOs;

namespace ExperimentRunner
{
    internal enum DataStoreType
    {
        BasicInMemory,
        RelationalNeo4j,
        TimeBucketedNeo4j,
        BTreePostgres,
        EFCorePostgres,
        ManyTablesPostgres,
        SingleTablePostgres,
        SpatialPostgres,
        SpatialManyTablesPostgres,
        GPUAccelerated
    }

    internal static class DataStoreTypeParser
    {
        public static IEventDataStore CreateDataStore(this DataStoreType storeType,
            IWeatherService weatherService,
            IRecalculateFlightEventPublisher recalculateFlightEventPublisher) => storeType switch
            {
                DataStoreType.BasicInMemory => new BasicEventDataStore.BasicEventDataStore(weatherService, recalculateFlightEventPublisher),
                DataStoreType.RelationalNeo4j => new Neo4jDataStore.RelationalNeo4jDataStore(weatherService, recalculateFlightEventPublisher),
                DataStoreType.TimeBucketedNeo4j => new Neo4jDataStore.TimeBucketedNeo4jDataStore(weatherService, recalculateFlightEventPublisher),
                DataStoreType.BTreePostgres => new OptimizedPostgreSQLDataStore.BTreePostgreSQLDataStore(weatherService, recalculateFlightEventPublisher),
                DataStoreType.EFCorePostgres => new SimplePostgreSQLDataStore.SimplePostgreSQLDataStore(weatherService, recalculateFlightEventPublisher),
                DataStoreType.ManyTablesPostgres => new ManyTablesPostgreSQLDataStore.ManyTablesPostgreSQLDatastore(weatherService, recalculateFlightEventPublisher),
                DataStoreType.SingleTablePostgres => new SingleTablePostgreSQLDataStore.SingleTablePostgreSQLDataStore(weatherService, recalculateFlightEventPublisher),
                DataStoreType.SpatialPostgres => new SpatialGISTPostgreSQL.SpatialPostgreSQLDatastore(weatherService, recalculateFlightEventPublisher),
                DataStoreType.SpatialManyTablesPostgres => new SpatialGISTManyTablesPostgreSQLDataStore.SpatialGISTManyTablesPostgreSQLDataStore(weatherService, recalculateFlightEventPublisher),
                DataStoreType.GPUAccelerated => new GPUAcceleratedEventDataStore.CUDAEventDataStore(weatherService, recalculateFlightEventPublisher),
                _ => throw new ArgumentException($"DataStore type {storeType} not found."),
            };
    }
}
