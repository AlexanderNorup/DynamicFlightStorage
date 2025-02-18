
using DynamicFlightStorageDTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DynamicFlightStorageSimulation;
public class FlightInjector
{
    private readonly SimulationEventBus _eventBus;
    private Queue<Flight>? _flights;
    private Dictionary<string, Flight>? _flightsById;
    private string _directoryPath;
    public FlightInjector(SimulationEventBus eventBus, string directoryPath)
    {
        _eventBus = eventBus;
        _directoryPath = directoryPath;
    }

    public void ResetReader()
    {
        _flights = null;
        _flightsById = null;
    }

    public Flight? GetFlightById(string flightId) => _flightsById?.GetValueOrDefault(flightId);

    public void SkipFlightsUntil(DateTime date, CancellationToken cancellationToken = default)
    {
        // This is ugly, but it works
        foreach (var _ in GetFlightsUntil(date, cancellationToken))
        {
            // Don't do anything
        }
    }

    public async Task PublishFlightsUntil(DateTime date, string experimentId, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        var flightsToPublish = GetFlightsUntil(date, cancellationToken).ToList();
        if (flightsToPublish.Count == 0)
        {
            //logger?.LogDebug("No flights to publish (until {Until}).", date);
            return;
        }

        logger?.LogDebug("Publishing {Count} flights (until {Until}).",
            flightsToPublish.Count,
            date);

        int flightCount = 0;
        foreach (var flightBatch in flightsToPublish)
        {
            await _eventBus.PublishFlightAsync(flightBatch, experimentId).ConfigureAwait(false);
            if (++flightCount % 1000 == 0)
            {
                logger?.LogDebug("Published {Count}/{Total} flights (until {Until}).",
                    flightCount,
                    flightsToPublish.Count,
                    date);
            }
        }
    }

    public IEnumerable<Flight> GetFlightsUntil(DateTime date, CancellationToken cancellationToken = default)
    {
        if (_flights is null)
        {
            _flights = new Queue<Flight>(DeserializeFlights(_directoryPath));
            _flightsById = _flights.ToDictionary(x => x.FlightIdentification);
        }

        if (_flights.Count < 1)
        {
            yield break;
        }

        var currentDate = _flights.Peek().DatePlanned;
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


    private List<Flight> DeserializeFlights(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine($"The path {directoryPath} is not a directory!");
        }
        string[] files = Directory.GetFiles(directoryPath, "*.json");
        Console.WriteLine($"Found {files.Length} files in {directoryPath}");

        var flightList = new List<Flight>();

        foreach (string file in files)
        {
            try
            {
                flightList.Add(JsonSerializer.Deserialize<Flight>(File.ReadAllText(file))
                    ?? throw new InvalidOperationException($"Deserializing {file} returned null?"));
            }
            catch (Exception)
            {
                Console.WriteLine($"Could not serialize flight from file {file}");
            }
        }

        return flightList.OrderBy(x => x.DatePlanned).ToList();
    }
}