using DynamicFlightStorageSimulation;
using FluentAssertions;

namespace SimulationTests;

public class WeatherInjectionTest
{
    private readonly string _filePathMetar = Path.Combine(AppContext.BaseDirectory, "Resources", "MetarTest.json");
    private readonly string _filePathMetarWithErrors = Path.Combine(AppContext.BaseDirectory, "Resources", "MetarTestErrors.json");

    private WeatherInjector _weatherInjector;
    
    [SetUp]
    public void Setup()
    {
        string jsonMetarWeather = File.ReadAllText(_filePathMetar);
        string jsonMetarWithErrorWeather = File.ReadAllText(_filePathMetarWithErrors);

        _weatherInjector = new WeatherInjector(null);
        _weatherInjector.AddWeather(jsonMetarWeather, jsonMetarWithErrorWeather);
    }

    [Test]
    public void TestNotEmpty()
    {
        _weatherInjector.GetWeatherList().Count.Should().BeGreaterThan(0);
    }
    
    [Test]
    public void TestAllEventsCreated()
    {
        _weatherInjector.GetWeatherList().Count.Should().Be(10);
    }
}