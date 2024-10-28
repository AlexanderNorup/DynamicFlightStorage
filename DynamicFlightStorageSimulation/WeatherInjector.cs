using DynamicFlightStorageDTOs;

namespace DynamicFlightStorageSimulation;

public class WeatherInjector
{
    private readonly SimulationEventBus _eventBus;
    private List<Weather> _weatherList = new();
    private int _counter;
    public WeatherInjector(SimulationEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /*
     * Accepts JSON data as strings - NOT file paths. Up for change
     */
    public void AddWeather(params string[] jsonStrings)
    {
        foreach (var jsonString in jsonStrings)
        {
            _weatherList.AddRange(WeatherCreator.ReadWeatherJson(jsonString));
        }

        _weatherList = _weatherList.OrderBy(x => x.ValidTo).ToList();
    }

    public List<Weather> GetWeatherList()
    {
        return _weatherList;
    }
    
    public async Task PublishWeatherUntil(DateTime date)
    {
        while (_counter < _weatherList.Count)
        {
            if (_weatherList[_counter].ValidTo > date)
            {
                return;
            }
            
            await _eventBus.PublishWeatherAsync(_weatherList[_counter]).ConfigureAwait(false);
            _counter++;
        }
    }

    public async Task PublishNextWeatherEvent()
    {
        await _eventBus.PublishWeatherAsync(_weatherList[_counter]).ConfigureAwait(false);
        _counter++;
    }
    
    public void RestartState()
    {
        _counter = 0;
    }
    
    public async Task PublishWeatherUntilStateless(DateTime date)
    {
        var weatherUntil = new List<Weather>();
        foreach (var weather in _weatherList)
        {
            if (weather.ValidTo > date)
            {
                return;
            }

            await _eventBus.PublishWeatherAsync(weather).ConfigureAwait(false);
        }
    }
    
}