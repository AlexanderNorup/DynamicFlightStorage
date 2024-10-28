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
        _flightInjector = new FlightInjector(null);
        _flightInjector.AddFlights(_directoryPath);
    }

    [Test]
    public void TestNotEmpty()
    {
        _flightInjector.GetFlights().Count.Should().BeGreaterThan(0);
    }
    
    [Test]
    public void TestAllFlightsCreated()
    {
        _flightInjector.GetFlights().Count.Should().Be(15);
    }
}