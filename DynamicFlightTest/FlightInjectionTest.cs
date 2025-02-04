using DynamicFlightStorageSimulation;
using FluentAssertions;

namespace SimulationTests;

public class FlightInjectionTest
{
    private readonly string _directoryPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Flights");
    private FlightInjector _flightInjector;

    [SetUp]
    public void Setup()
    {
        _flightInjector = new FlightInjector(null, _directoryPath);
    }

    [Test]
    public void TestNotEmpty()
    {
        _flightInjector.GetFlightsUntill(DateTime.MaxValue).Should().HaveCountGreaterThan(0);
    }

    [Test]
    public void TestAllFlightsCreated()
    {
        _flightInjector.GetFlightsUntill(DateTime.MaxValue).Should().HaveCount(10);
    }
}