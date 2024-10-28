namespace DynamicFlightStorageSimulation;

using DynamicFlightStorageDTOs;
using System.Text.Json;

public class FlightInjector
{
    private readonly SimulationEventBus _eventBus;
    private List<Flight> _flightList = new();
    private int _counter;
    public FlightInjector(SimulationEventBus eventBus)
    {
        _eventBus = eventBus;
    }
    
    public void AddFlights(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine($"The path {directoryPath} is not a directory!");
        }
        string[] files = Directory.GetFiles(directoryPath, "*.json");

        foreach (string file in files)
        {
            try
            {
                _flightList.Add(JsonSerializer.Deserialize<Flight>(File.ReadAllText(file)));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not serialize flight from file {file}");
            }
            
        }

        _flightList = _flightList.OrderBy(x => x.ScheduledTimeOfArrival).ToList();
    }
    
    public List<Flight> GetFlights()
    {
        return _flightList;
    }
    
    public async Task PublishFlightsUntil(DateTime date)
    {
        while (_counter < _flightList.Count)
        {
            if (_flightList[_counter].ScheduledTimeOfArrival > date)
            {
                return;
            }
            
            await _eventBus.PublishFlightAsync(_flightList[_counter]).ConfigureAwait(false);
            _counter++;
        }
    }

    public async Task PublishNextWeatherEvent()
    {
        await _eventBus.PublishFlightAsync(_flightList[_counter]).ConfigureAwait(false);
        _counter++;
    }
    
    public void RestartState()
    {
        _counter = 0;
    }
}