using DynamicFlightStorageDTOs;
using FluentAssertions;

namespace SimulationTests;
using DynamicFlightStorageSimulation;

public class WeatherCreationTest
{
    private readonly string _filePathTaf = Path.Combine(AppContext.BaseDirectory, "Resources", "TafTest.json");
    private readonly string _filePathTafWithErrors = Path.Combine(AppContext.BaseDirectory, "Resources", "TafTestErrors.json");
    private readonly string _filePathMetar = Path.Combine(AppContext.BaseDirectory, "Resources", "MetarTest.json");
    private readonly string _filePathMetarWithErrors = Path.Combine(AppContext.BaseDirectory, "Resources", "MetarTestErrors.json");
    private List<Weather> _tafWeatherList;
    private List<Weather> _tafWithErrorWeatherList;
    private List<Weather> _metarWeatherList;
    private List<Weather> _metarWithErrorWeatherList;
    [SetUp]
    public void Setup()
    {
        string jsonTafWeather = File.ReadAllText(_filePathTaf);
        string jsonTafWithErrorWeather = File.ReadAllText(_filePathTafWithErrors);
        string jsonMetarWeather = File.ReadAllText(_filePathMetar);
        string jsonMetarWithErrorWeather = File.ReadAllText(_filePathMetarWithErrors);
        
        _tafWeatherList = WeatherCreator.ReadWeatherJson(jsonTafWeather);
        _tafWithErrorWeatherList = WeatherCreator.ReadWeatherJson(jsonTafWithErrorWeather);
        _metarWeatherList = WeatherCreator.ReadWeatherJson(jsonMetarWeather);
        _metarWithErrorWeatherList = WeatherCreator.ReadWeatherJson(jsonMetarWithErrorWeather);
    }

    [Test]
    public void TafObjectsAreCreated()
    {
        _tafWeatherList.Should().HaveCount(7);
    }

    [Test]
    public void FirstTafIsHandled()
    {
        _tafWeatherList[0].Airport.Should().Be("SAOU");
        _tafWeatherList[0].WeatherLevel.Should().Be(WeatherCategory.LIFR);
        _tafWeatherList[0].ValidFrom.Should().Be(DateTime.Parse("2024-10-10T02:00:00Z"));
        _tafWeatherList[0].ValidTo.Should().Be(DateTime.Parse("2024-10-10T12:00:00Z"));
        _tafWeatherList[0].Id.Should().Be("1d467ada-6b87-4673-b088-0be32d21a001_0");
        
        _tafWeatherList[1].Airport.Should().Be("SAOU");
        _tafWeatherList[1].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWeatherList[1].ValidFrom.Should().Be(DateTime.Parse("2024-10-10T12:00:00Z"));
        _tafWeatherList[1].ValidTo.Should().Be(DateTime.Parse("2024-10-10T14:00:00Z"));
        _tafWeatherList[1].Id.Should().Be("1d467ada-6b87-4673-b088-0be32d21a001_1");
        
        _tafWeatherList[2].Airport.Should().Be("SAOU");
        _tafWeatherList[2].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWeatherList[2].ValidFrom.Should().Be(DateTime.Parse("2024-10-10T14:00:00Z"));
        _tafWeatherList[2].ValidTo.Should().Be(DateTime.Parse("2024-10-10T21:00:00Z"));
        _tafWeatherList[2].Id.Should().Be("1d467ada-6b87-4673-b088-0be32d21a001_2");
        
        _tafWeatherList[3].Airport.Should().Be("SAOU");
        _tafWeatherList[3].WeatherLevel.Should().Be(WeatherCategory.LIFR);
        _tafWeatherList[3].ValidFrom.Should().Be(DateTime.Parse("2024-10-10T21:00:00Z"));
        _tafWeatherList[3].ValidTo.Should().Be(DateTime.Parse("2024-10-11T00:00:00Z"));
        _tafWeatherList[3].Id.Should().Be("1d467ada-6b87-4673-b088-0be32d21a001_3");
    }
    
    [Test]
    public void LastTafIsHandled()
    {
        _tafWeatherList[4].Airport.Should().Be("HJJJ");
        _tafWeatherList[4].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWeatherList[4].ValidFrom.Should().Be(DateTime.Parse("2024-10-10T06:00:00Z"));
        _tafWeatherList[4].ValidTo.Should().Be(DateTime.Parse("2024-10-10T11:00:00Z"));
        _tafWeatherList[4].Id.Should().Be("a20a02e8-c8c2-43d8-80e5-1d7ba33192b1_0");
        
        _tafWeatherList[5].Airport.Should().Be("HJJJ");
        _tafWeatherList[5].WeatherLevel.Should().Be(WeatherCategory.MVFR);
        _tafWeatherList[5].ValidFrom.Should().Be(DateTime.Parse("2024-10-10T11:00:00Z"));
        _tafWeatherList[5].ValidTo.Should().Be(DateTime.Parse("2024-10-10T14:00:00Z"));
        _tafWeatherList[5].Id.Should().Be("a20a02e8-c8c2-43d8-80e5-1d7ba33192b1_1");
        
        _tafWeatherList[6].Airport.Should().Be("HJJJ");
        _tafWeatherList[6].WeatherLevel.Should().Be(WeatherCategory.MVFR);
        _tafWeatherList[6].ValidFrom.Should().Be(DateTime.Parse("2024-10-10T14:00:00Z"));
        _tafWeatherList[6].ValidTo.Should().Be(DateTime.Parse("2024-10-11T12:00:00Z"));
        _tafWeatherList[6].Id.Should().Be("a20a02e8-c8c2-43d8-80e5-1d7ba33192b1_2");
    }

    [Test]
    public void TafWithErrorsIsHandled()
    {
        _tafWithErrorWeatherList.Should().HaveCount(8);
        
        _tafWithErrorWeatherList[0].Airport.Should().Be("KMUO");
        _tafWithErrorWeatherList[0].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWithErrorWeatherList[0].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T00:00:00Z"));
        _tafWithErrorWeatherList[0].ValidTo.Should().Be(DateTime.Parse("2024-08-03T13:00:00Z"));
        
        _tafWithErrorWeatherList[1].Airport.Should().Be("KMUO");
        _tafWithErrorWeatherList[1].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWithErrorWeatherList[1].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T13:00:00Z"));
        _tafWithErrorWeatherList[1].ValidTo.Should().Be(DateTime.Parse("2024-08-03T15:00:00Z"));
        
        _tafWithErrorWeatherList[2].Airport.Should().Be("KMUO");
        _tafWithErrorWeatherList[2].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWithErrorWeatherList[2].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T15:00:00Z"));
        _tafWithErrorWeatherList[2].ValidTo.Should().Be(DateTime.Parse("2024-08-04T00:00:00Z"));
        
        _tafWithErrorWeatherList[3].Airport.Should().Be("KHMN");
        _tafWithErrorWeatherList[3].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWithErrorWeatherList[3].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T00:00:00Z"));
        _tafWithErrorWeatherList[3].ValidTo.Should().Be(DateTime.Parse("2024-08-03T00:00:00Z"));
        
        _tafWithErrorWeatherList[4].Airport.Should().Be("KHMN");
        _tafWithErrorWeatherList[4].WeatherLevel.Should().Be(WeatherCategory.MVFR);
        _tafWithErrorWeatherList[4].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T00:00:00Z"));
        _tafWithErrorWeatherList[4].ValidTo.Should().Be(DateTime.Parse("2024-08-03T01:00:00Z"));
        
        _tafWithErrorWeatherList[5].Airport.Should().Be("KHMN");
        _tafWithErrorWeatherList[5].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWithErrorWeatherList[5].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T01:00:00Z"));
        _tafWithErrorWeatherList[5].ValidTo.Should().Be(DateTime.Parse("2024-08-03T02:00:00Z"));
        
        _tafWithErrorWeatherList[6].Airport.Should().Be("KHMN");
        _tafWithErrorWeatherList[6].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWithErrorWeatherList[6].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T02:00:00Z"));
        _tafWithErrorWeatherList[6].ValidTo.Should().Be(DateTime.Parse("2024-08-03T03:00:00Z"));
        
        _tafWithErrorWeatherList[7].Airport.Should().Be("KHMN");
        _tafWithErrorWeatherList[7].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWithErrorWeatherList[7].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T03:00:00Z"));
        _tafWithErrorWeatherList[7].ValidTo.Should().Be(DateTime.Parse("2024-08-03T23:00:00Z"));
    }

    [Test]
    public void MetarObjectsCreated()
    {
        _metarWeatherList.Should().HaveCount(2);
    }
    
    [Test]
    public void MetarWeatherIsHandled()
    {
        _metarWeatherList[0].Id.Should().Be("f3263fcb-2a46-4b0d-8021-774196232374");
        _metarWeatherList[0].Airport.Should().Be("BIGJ");
        _metarWeatherList[0].WeatherLevel.Should().Be(WeatherCategory.MVFR);
        _metarWeatherList[0].ValidFrom.Should().Be(DateTime.Parse("2024-10-11T14:03:00Z"));
        _metarWeatherList[0].ValidTo.Should().Be(DateTime.Parse("2024-10-11T14:03:00Z"));
        
        _metarWeatherList[1].Id.Should().Be("265f021a-ff72-477d-aff5-72879ebb2439");
        _metarWeatherList[1].Airport.Should().Be("SBML");
        _metarWeatherList[1].WeatherLevel.Should().Be(WeatherCategory.MVFR);
        _metarWeatherList[1].ValidFrom.Should().Be(DateTime.Parse("2024-10-11T14:17:00Z"));
        _metarWeatherList[1].ValidTo.Should().Be(DateTime.Parse("2024-10-11T14:17:00Z"));
    }
    
    [Test]
    public void MetarWithErrorsObjectsCreated()
    {
        _metarWithErrorWeatherList.Should().HaveCount(4);
    }
    
    [Test]
    public void MetarWithErrorsWeatherIsHandled()
    {
        _metarWithErrorWeatherList[0].Airport.Should().Be("VCRI");
        _metarWithErrorWeatherList[0].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _metarWithErrorWeatherList[0].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T21:10:00Z"));
        _metarWithErrorWeatherList[0].ValidTo.Should().Be(DateTime.Parse("2024-08-03T21:10:00Z"));
        
        _metarWithErrorWeatherList[1].Airport.Should().Be("KSXU");
        _metarWithErrorWeatherList[1].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _metarWithErrorWeatherList[1].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T21:15:00Z"));
        _metarWithErrorWeatherList[1].ValidTo.Should().Be(DateTime.Parse("2024-08-03T21:15:00Z"));
        
        _metarWithErrorWeatherList[2].Airport.Should().Be("SBIZ");
        _metarWithErrorWeatherList[2].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _metarWithErrorWeatherList[2].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T21:25:00Z"));
        _metarWithErrorWeatherList[2].ValidTo.Should().Be(DateTime.Parse("2024-08-03T21:25:00Z"));
        
        _metarWithErrorWeatherList[3].Airport.Should().Be("VISP");
        _metarWithErrorWeatherList[3].WeatherLevel.Should().Be(WeatherCategory.MVFR);
        _metarWithErrorWeatherList[3].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T21:30:00Z"));
        _metarWithErrorWeatherList[3].ValidTo.Should().Be(DateTime.Parse("2024-08-03T21:30:00Z"));
    }
}