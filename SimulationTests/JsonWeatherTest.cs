using DynamicFlightStorageDTOs;

namespace SimulationTests;
using DynamicFlightStorageSimulation;

public class JsonWeatherTest
{
    private string jsonWeather;
    private string filePath = "Resources/TafTest.json";
    [SetUp]
    public void Setup()
    {
        try
        {
            jsonWeather = File.ReadAllText(filePath);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Could not open file: {e.Message}");
        }
    }

    [Test]
    public void JsonIsRead()
    {
        List<Weather> weatherList = WeatherCreator.ReadWeatherJson(jsonWeather);
        
        Assert.That(weatherList[0].Airport, Is.EqualTo("KMUO"));
        Assert.That(weatherList[0].WeatherLevel, Is.EqualTo(WeatherCategory.VFR));
        Assert.That(weatherList[0].ValidFrom, Is.EqualTo(DateTime.Parse("2024-08-03T00:00:00Z")));
        Assert.That(weatherList[0].ValidTo, Is.EqualTo(DateTime.Parse("2024-08-03T13:00:00Z")));
    }
}