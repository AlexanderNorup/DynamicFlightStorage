using DynamicFlightStorageSimulation;
using FluentAssertions;

namespace SimulationTests;

[Ignore("These tests only works when resources folder contains specific sub directories with specific files.")]
public class WeatherInjectionTest
{
    private readonly string _metarPath = Path.Combine(AppContext.BaseDirectory, "Resources", "metar");
    private readonly string _tafPath = Path.Combine(AppContext.BaseDirectory, "Resources", "taf");

    private WeatherInjector _weatherInjector;
    private Queue<string> _metarFilesQueue;
    private Queue<string> _tafFilesQueue;


    [SetUp]
    public void Setup()
    {
        _weatherInjector = new WeatherInjector(null!, _metarPath, _tafPath);
        _metarFilesQueue = _weatherInjector.GetMetarFiles();
        _tafFilesQueue = _weatherInjector.GetTafFiles();
    }

    [Test]
    public void TestFilesFound()
    {
        _metarFilesQueue.Count.Should().Be(5);
        _tafFilesQueue.Count.Should().Be(5);
    }

    [Test]
    public void TestFilesInOrder()
    {
        _metarFilesQueue.Dequeue().Should().EndWith("metar2024-08-03T22.json");
        _metarFilesQueue.Dequeue().Should().EndWith("metar2024-08-03T23.json");
        _metarFilesQueue.Dequeue().Should().EndWith("metar2024-08-04T00.json");
        _metarFilesQueue.Dequeue().Should().EndWith("metar2024-08-04T01.json");
        _metarFilesQueue.Dequeue().Should().EndWith("metar2024-08-04T02.json");

        _tafFilesQueue.Dequeue().Should().EndWith("taf2024-08-03T22.json");
        _tafFilesQueue.Dequeue().Should().EndWith("taf2024-08-03T23.json");
        _tafFilesQueue.Dequeue().Should().EndWith("taf2024-08-04T00.json");
        _tafFilesQueue.Dequeue().Should().EndWith("taf2024-08-04T01.json");
        _tafFilesQueue.Dequeue().Should().EndWith("taf2024-08-04T02.json");
    }
}