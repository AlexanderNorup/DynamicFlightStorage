using DynamicFlightStorageSimulation.DataCollection;
using DynamicFlightStorageSimulation.ExperimentOrchestrator;
using DynamicFlightStorageSimulation.ExperimentOrchestrator.DataCollection;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;
using System.Text;
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
                if (httpContext.Request.Query.ContainsKey("json"))
                {
                    return Results.File(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(logList)),
                        "application/json",
                        $"flightlogs_{logs.ExperimentId}_{logs.ClientId}.json");
                }

                StringBuilder csvBuilder = new StringBuilder();
                csvBuilder.AppendLine("FlightId,SentTimestamp,ReceivedTimestamp");
                foreach (var line in logList)
                {
                    csvBuilder.AppendLine($"{line.Flight.FlightIdentification},{line.SentTimestamp:o},{line.ReceivedTimestamp:o}");
                }

                return Results.File(System.Text.Encoding.UTF8.GetBytes(csvBuilder.ToString()),
                    "text/csv",
                    $"flightlogs_{logs.ExperimentId}_{logs.ClientId}.csv");
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

                if (httpContext.Request.Query.ContainsKey("json"))
                {
                    return Results.File(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(logList)),
                        "application/json",
                        $"weatherlogs_{logs.ExperimentId}_{logs.ClientId}.json");
                }

                StringBuilder csvBuilder = new StringBuilder();
                csvBuilder.AppendLine("WeatherId,SentTimestamp,ReceivedTimestamp");
                foreach (var line in logList)
                {
                    csvBuilder.AppendLine($"{line.Weather.Id},{line.SentTimestamp:o},{line.ReceivedTimestamp:o}");
                }

                return Results.File(System.Text.Encoding.UTF8.GetBytes(csvBuilder.ToString()),
                    "text/csv",
                    $"weatherlogs_{logs.ExperimentId}_{logs.ClientId}.csv");
            }).WithName("weatherlog_download");

            app.MapGet("/api/lag/{id:int}", async (DataCollectionContext context, HttpContext httpContext, int id) =>
            {
                var logs = await context.ExperimentClientResults
                    .Where(x => x.Id == id)
                    .Include(x => x.ExperimentResult)
                    .Select(x => new { x.ExperimentResult!.Id, x.ClientId, x.LagData })
                    .FirstOrDefaultAsync();

                if (logs is null)
                {
                    return Results.NotFound("Client result not found");
                }

                if (logs.LagData is null)
                {
                    return Results.NotFound("No log data available for experiment-result ");
                }

                if (!logs.LagData.TryGetLagDataFromCompressedBytes(out var lagData))
                {
                    return Results.NotFound("No log data could not be decompressed");
                }

                StringBuilder csvBuilder = new StringBuilder();
                csvBuilder.AppendLine("Timestamp,WeatherLag,FlightLag");
                foreach (var line in lagData)
                {
                    csvBuilder.AppendLine($"{line.Timestamp:o},{line.WeatherLag},{line.FlightLag}");
                }

                return Results.File(System.Text.Encoding.UTF8.GetBytes(csvBuilder.ToString()),
                    "text/csv",
                    $"laglogs_{logs.Id}_{logs.ClientId}.csv");
            }).WithName("laglog_download");

            app.MapGet("/api/recalculations/{experimentId}/{clientId}", async (DataCollectionContext context, HttpContext httpContext, string experimentId, string clientId) =>
            {
                var logs = await context.RecalculationEventLogs
                    .OrderBy(x => x.Id)
                    .Where(x => x.ExperimentId == experimentId && x.ClientId == clientId)
                    .Select(x => new { x.FlightId, x.TriggeredBy, x.LagInMilliseconds, x.UtcTimeStamp })
                    .ToListAsync().ConfigureAwait(false);

                if (logs is null)
                {
                    return Results.NotFound("Recalculation events not found result not found");
                }

                if (logs.Count is 0)
                {
                    return Results.NotFound("This experimentid/clientid combination did not return any recalculationevents");
                }

                StringBuilder csvBuilder = new StringBuilder();
                csvBuilder.AppendLine("FlightId,TriggeredBy,LagMs,UtcTimeStamp");
                foreach (var line in logs)
                {
                    csvBuilder.AppendLine($"{line.FlightId},{line.TriggeredBy},{line.LagInMilliseconds.ToString(CultureInfo.InvariantCulture)},{DateTime.SpecifyKind(line.UtcTimeStamp,DateTimeKind.Utc):o}");
                }

                return Results.File(System.Text.Encoding.UTF8.GetBytes(csvBuilder.ToString()),
                    "text/csv",
                    $"recalculations_{experimentId}_{clientId}.csv");
            }).WithName("recalculations_download");

            app.MapGet("/api/experiment/{experimentId}/{clientId}", async (DataCollectionContext context, HttpContext httpContext, LinkGenerator linkGenerator, string experimentId, string clientId) =>
            {
                var experimentData = await context.ExperimentClientResults
                    .OrderBy(x => x.Id)
                    .Include(x => x.ExperimentResult)
                        .ThenInclude(x => x!.Experiment)
                    .Include(x => x.LatencyTest)
                    .Where(x => x.ExperimentResult!.ExperimentId == experimentId && x.ClientId == clientId)
                    .Select(x => new
                    {
                        x.ExperimentResult!.ExperimentId,
                        x.ExperimentResult.ExperimentRunDescription,
                        x.ExperimentResult.ExperimentSuccess,
                        x.ExperimentResult.UTCStartTime,
                        x.ExperimentResult.UTCEndTime,
                        ClientResultId = x.Id,
                        x.ClientId,
                        x.DataStoreType,
                        x.MaxWeatherConsumerLag,
                        x.MaxFlightConsumerLag,
                        x.ExperimentResult.Experiment,
                        x.LatencyTest
                    })
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (experimentData is null)
                {
                    return Results.NotFound("Experiment Result not found");
                }

                var flightEventLog = await context.FlightEventLogs
                    .Where(x => x.ExperimentId == experimentId && x.ClientId == clientId)
                    .Select(x => new { x.Id })
                    .FirstOrDefaultAsync();

                var weatherEventLog = await context.WeatherEventLogs
                    .Where(x => x.ExperimentId == experimentId && x.ClientId == clientId)
                    .Select(x => new { x.Id })
                    .FirstOrDefaultAsync();

                var response = new
                {
                    ExperimentData = experimentData,
                    Links = new
                    {
                        FlightLogs = flightEventLog is not null ? linkGenerator.GetPathByName(httpContext, "flightlog_download", new { id = flightEventLog.Id }) : null,
                        WeatherLogs = weatherEventLog is not null ? linkGenerator.GetPathByName(httpContext, "weatherlog_download", new { id = weatherEventLog.Id }) : null,
                        LagLogs = linkGenerator.GetPathByName(httpContext, "laglog_download", new { id = experimentData.ClientResultId }),
                        RecalculationLogs = linkGenerator.GetPathByName(httpContext, "recalculations_download", new { experimentId, clientId })
                    }
                };

                if (httpContext.Request.Query.ContainsKey("dl"))
                {
                    return Results.File(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response)),
                        "application/json",
                        $"flightlogs_{experimentData.ExperimentId}_{experimentData.ClientId}.json");
                }

                return Results.Ok(response);
            }).WithName("experiment_download");
        }
    }
}
