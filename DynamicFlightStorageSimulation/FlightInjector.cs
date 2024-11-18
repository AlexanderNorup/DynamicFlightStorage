namespace DynamicFlightStorageSimulation;

using DynamicFlightStorageDTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;

public class FlightInjector
{
    private readonly SimulationEventBus _eventBus;
    private Queue<Flight>? _flights;
    private string _directoryPath;
    public FlightInjector(SimulationEventBus eventBus, string directoryPath)
    {
        _eventBus = eventBus;
        _directoryPath = directoryPath;
    }

    public void ResetReader()
    {
        _flights = null;
    }

    private List<Flight> DeserializeFlights(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine($"The path {directoryPath} is not a directory!");
        }
        string[] files = Directory.GetFiles(directoryPath, "*.json");

        var flightList = new List<Flight>();

        foreach (string file in files)
        {
            try
            {
                flightList.Add(JsonSerializer.Deserialize<Flight>(File.ReadAllText(file))
                    ?? throw new InvalidOperationException($"Deserializing {file} returned null?"));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not serialize flight from file {file}");
            }
        }

        return flightList.OrderBy(x => x.DatePlanned).ToList();
    }

    public async Task PublishFlightsUntil(DateTime date, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        var flightsToPublish = GetFlightsUntill(date, cancellationToken).ToList();
        if (flightsToPublish.Count == 0)
        {
            //logger?.LogDebug("No flights to publish (untill {Untill}).", date);
            return;
        }

        logger?.LogDebug("Publishing {Count} flights (until {Untill}).",
            flightsToPublish.Count,
            date);

        int flightCount = 0;
        foreach (var flightBatch in flightsToPublish)
        {
            await _eventBus.PublishFlightAsync(flightBatch).ConfigureAwait(false);
            if (++flightCount % 10 == 0)
            {
                logger?.LogDebug("Published {Count}/{Total} flights (untill {Untill}).",
                    flightCount,
                    flightsToPublish.Count,
                    date);
            }
        }
    }

    public IEnumerable<Flight> GetFlightsUntill(DateTime date, CancellationToken cancellationToken = default)
    {
        if (_flights is null)
        {
            _flights = new Queue<Flight>(DeserializeFlights(_directoryPath));
        }

        if (_flights.Count < 1)
        {
            yield break;
        }

        var currentDate = _flights.Peek().DatePlanned;
        int i = 0;
        while (currentDate < date && !cancellationToken.IsCancellationRequested)
        {
            yield return _flights.Dequeue();

            if (_flights.Count < 1)
            {
                yield break;
            }
            currentDate = _flights.Peek().DatePlanned;
        }
    }
}