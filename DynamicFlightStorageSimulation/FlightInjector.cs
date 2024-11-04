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

        return flightList;
    }
    
    public async Task PublishFlightsUntil(DateTime date)
    {
        if (_flights is null)
        {
            _flights = new Queue<Flight>(DeserializeFlights(_directoryPath));
        }

        if (_flights.Count < 1)
        {
            return;
        }
        
        
        var currentDate = _flights.Peek().ScheduledTimeOfArrival;

        while (currentDate < date)
        {
            await _eventBus.PublishFlightAsync(_flights.Dequeue()).ConfigureAwait(false);
            if (_flights.Count < 1)
            {
                return;
            }
            currentDate = _flights.Peek().ScheduledTimeOfArrival;
        }
    }

    public Queue<Flight> GetFlights()
    {
        return _flights;
    }
}