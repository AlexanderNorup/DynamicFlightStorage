using Neo4j.Driver;

namespace Neo4jDataStore
{
    internal static class CommonGraphDatabaseCommands
    {
        internal static async Task CreateOrUpdateFlight(IAsyncQueryRunner tx, string flightId)
        {
            const string Query =
                """
                MERGE (f:Flight {id:$flightId})
                SET f.recalculating = false;
                """;
            await tx.RunAsync(Query, new { flightId }).ConfigureAwait(false);
        }

        internal static async Task SetRecalculation(IAsyncQueryRunner tx, string[] flightIds)
        {
            const string Query =
                """
                MATCH (f:Flight)
                WHERE f.id IN $flightIds
                SET f.recalculating = true
                """;
            await tx.RunAsync(Query, new { flightIds }).ConfigureAwait(false);
        }
    }
}
