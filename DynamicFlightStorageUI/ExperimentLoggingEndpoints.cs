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
            app.MapGet("/api/flightlogs/{id:int}", async (DataCollectionContext context, HttpContext httpContext, int id) =>
            {
                var logs = await context.FlightEventLogs
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (logs is null)
                {
                    return Results.NotFound();
                }

                var decompressed = CompressionHelpers.Decompress(logs.FlightData);

                var logList = MessagePackSerializer.Deserialize<LinkedList<ConsumerDataLogger.FlightLog>>(decompressed, ConsumerDataLogger.MessagePackOptions);

                return Results.File(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(logList)),
                    "application/json",
                    $"flightlogs_{logs.ExperimentId}_{logs.ClientId}.json");
            }).WithName("flightlog_download");

            app.MapGet("/api/weatherlogs/{id:int}", async (DataCollectionContext context, HttpContext httpContext, int id) =>
            {
                var logs = await context.WeatherEventLogs
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (logs is null)
                {
                    return Results.NotFound();
                }

                var decompressed = CompressionHelpers.Decompress(logs.WeatherData);

                var logList = MessagePackSerializer.Deserialize<LinkedList<ConsumerDataLogger.WeatherLog>>(decompressed, ConsumerDataLogger.MessagePackOptions);

                return Results.File(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(logList)),
                    "application/json",
                    $"weatherlogs_{logs.ExperimentId}_{logs.ClientId}.json");
            }).WithName("weatherlog_download");
        }
    }
}
