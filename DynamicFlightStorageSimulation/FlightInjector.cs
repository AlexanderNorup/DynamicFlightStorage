namespace DynamicFlightStorageSimulation;

using DynamicFlightStorageDTOs;
using System.Text.Json;

public class FlightInjector
{
    private readonly SimulationEventBus _eventBus;
    private Queue<Flight>? _flights;
    private int _counter;
    private string _directoryPath;
    public FlightInjector(SimulationEventBus eventBus, string directoryPath)
    {
        _eventBus = eventBus;
        _directoryPath = directoryPath;

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
                flightList.Add(JsonSerializer.Deserialize<Flight>(File.ReadAllText(file)));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not serialize flight from file {file}");
            }

        }

        //TODO: This should be sorted by DatePlanned instead
        return flightList.OrderBy(x => x.ScheduledTimeOfDeparture).ToList();
    }

    public async Task PublishFlightsUntil(DateTime date, CancellationToken cancellationToken = default)
    {
        if (_flights is null)
        {
            _flights = new Queue<Flight>(DeserializeFlights(_directoryPath));
        }

        if (_flights.Count < 1)
        {
            return;
        }

        var currentDate = _flights.Peek().ScheduledTimeOfDeparture;
        int i = 0;
        while (currentDate < date && !cancellationToken.IsCancellationRequested)
        {
            await _eventBus.PublishFlightAsync(_flights.Dequeue()).ConfigureAwait(false);
            if (_flights.Count < 1)
            {
                return;
            }
            currentDate = _flights.Peek().ScheduledTimeOfDeparture;
            if (i++ > 30)
            {
                return;
            }
        }
    }

    public Queue<Flight> GetFlights()
    {
        return _flights;
    }
}