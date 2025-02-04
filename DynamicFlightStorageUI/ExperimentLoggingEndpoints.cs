using DynamicFlightStorageSimulation.DataCollection;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DynamicFlightStorageUI
{
    public static class ExperimentLoggingEndpoints
    {
        public static void AddExperimentLogEndpoints(this WebApplication app)
        {
            // TODO: Either don't deploy this somewhere public or add authentication
            app.MapGet("/api/experiment", async (DataCollectionContext context, HttpContext httpContext) =>
            {
                httpContext.Response.ContentType = "application/json";
                return Results.Ok(await context.Experiments.ToListAsync());
            });

            app.MapGet("/api/experiment/{experimentId}/flightlogs", async (DataCollectionContext context, HttpContext httpContext, string experimentId) =>
            {
                var logs = await context.FlightEventLogs
                    .Where(log => log.ExperimentId == experimentId)
                    .OrderBy(log => log.UtcTimeStamp)
                    .FirstOrDefaultAsync();

                if (logs is null)
                {
                    return Results.NotFound();
                }

                var logList = MessagePackSerializer.Deserialize<LinkedList<ConsumerDataLogger.FlightLog>>(logs.FlightData, ConsumerDataLogger.MessagePackOptions);

                return Results.File(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(logList)),
                    "application/json",
                    $"flightlogs_{experimentId}.json");
            });

            app.MapGet("/api/experiment/{experimentId}/weatherlogs", async (DataCollectionContext context, HttpContext httpContext, string experimentId) =>
            {
                var logs = await context.WeatherEventLogs
                    .Where(log => log.ExperimentId == experimentId)
                    .OrderBy(log => log.UtcTimeStamp)
                    .FirstOrDefaultAsync();

                if (logs is null)
                {
                    return Results.NotFound();
                }

                var logList = MessagePackSerializer.Deserialize<LinkedList<ConsumerDataLogger.WeatherLog>>(logs.WeatherData, ConsumerDataLogger.MessagePackOptions);

                return Results.File(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(logList)),
                    "application/json",
                    $"weatherlogs_{experimentId}.json");
            });
        }
    }
}
