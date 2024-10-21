using DynamicFlightStorageDTOs;
using FluentAssertions;

namespace SimulationTests;
using DynamicFlightStorageSimulation;
public class LoadWholeJsonTest
{
    private readonly string _filePathTaf = Path.Combine(AppContext.BaseDirectory, "Resources", "taf2024-08-03T23.json");
    private readonly string _filePathMetar = Path.Combine(AppContext.BaseDirectory, "Resources", "metar2024-08-03T23.json");
    private List<Weather> _tafWeatherList;
    private List<Weather> _metarWeatherList;
    [SetUp]
    public void setup()
    {
        string jsonTafWeather = File.ReadAllText(_filePathTaf);
        string jsonMetarWeather = File.ReadAllText(_filePathMetar);
        
        _tafWeatherList = WeatherCreator.ReadWeatherJson(jsonTafWeather);
        _metarWeatherList = WeatherCreator.ReadWeatherJson(jsonMetarWeather);
    }

    [Test]
    public void test()
    {
        Console.WriteLine(_tafWeatherList.Count);
        Console.WriteLine(_metarWeatherList.Count);
    }
}