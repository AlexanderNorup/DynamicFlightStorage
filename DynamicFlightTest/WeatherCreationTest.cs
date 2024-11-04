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
        _tafWeatherList.Should().HaveCount(26);
    }

    [Test]
    public void FirstTafIsHandled()
    {
        _tafWeatherList[0].Airport.Should().Be("KMUO");
        _tafWeatherList[0].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWeatherList[0].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T00:00:00Z"));
        _tafWeatherList[0].ValidTo.Should().Be(DateTime.Parse("2024-08-03T13:00:00Z"));
        
        _tafWeatherList[1].Airport.Should().Be("KMUO");
        _tafWeatherList[1].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWeatherList[1].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T13:00:00Z"));
        _tafWeatherList[1].ValidTo.Should().Be(DateTime.Parse("2024-08-03T14:00:00Z"));
        
        _tafWeatherList[2].Airport.Should().Be("KMUO");
        _tafWeatherList[2].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWeatherList[2].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T14:00:00Z"));
        _tafWeatherList[2].ValidTo.Should().Be(DateTime.Parse("2024-08-03T15:00:00Z"));
        
        _tafWeatherList[3].Airport.Should().Be("KMUO");
        _tafWeatherList[3].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWeatherList[3].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T15:00:00Z"));
        _tafWeatherList[3].ValidTo.Should().Be(DateTime.Parse("2024-08-04T00:00:00Z"));
    }
    
    [Test]
    public void LastTafIsHandled()
    {
        _tafWeatherList[20].Airport.Should().Be("KHMN");
        _tafWeatherList[20].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWeatherList[20].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T00:00:00Z"));
        _tafWeatherList[20].ValidTo.Should().Be(DateTime.Parse("2024-08-03T00:00:00Z"));
        
        _tafWeatherList[21].Airport.Should().Be("KHMN");
        _tafWeatherList[21].WeatherLevel.Should().Be(WeatherCategory.MVFR);
        _tafWeatherList[21].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T00:00:00Z"));
        _tafWeatherList[21].ValidTo.Should().Be(DateTime.Parse("2024-08-03T01:00:00Z"));
        
        _tafWeatherList[22].Airport.Should().Be("KHMN");
        _tafWeatherList[22].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWeatherList[22].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T01:00:00Z"));
        _tafWeatherList[22].ValidTo.Should().Be(DateTime.Parse("2024-08-03T02:00:00Z"));
        
        _tafWeatherList[23].Airport.Should().Be("KHMN");
        _tafWeatherList[23].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWeatherList[23].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T02:00:00Z"));
        _tafWeatherList[23].ValidTo.Should().Be(DateTime.Parse("2024-08-03T03:00:00Z"));
        
        _tafWeatherList[24].Airport.Should().Be("KHMN");
        _tafWeatherList[24].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWeatherList[24].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T03:00:00Z"));
        _tafWeatherList[24].ValidTo.Should().Be(DateTime.Parse("2024-08-03T16:00:00Z"));
        
        _tafWeatherList[25].Airport.Should().Be("KHMN");
        _tafWeatherList[25].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _tafWeatherList[25].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T16:00:00Z"));
        _tafWeatherList[25].ValidTo.Should().Be(DateTime.Parse("2024-08-03T23:00:00Z"));
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
        _metarWeatherList.Should().HaveCount(6);
    }
    
    [Test]
    public void MetarWeatherIsHandled()
    {
        _metarWeatherList[0].Airport.Should().Be("KMMK");
        _metarWeatherList[0].WeatherLevel.Should().Be(WeatherCategory.IFR);
        _metarWeatherList[0].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T21:04:00Z"));
        _metarWeatherList[0].ValidTo.Should().Be(DateTime.Parse("2024-08-03T21:04:00Z"));
        
        _metarWeatherList[1].Airport.Should().Be("VCRI");
        _metarWeatherList[1].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _metarWeatherList[1].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T21:10:00Z"));
        _metarWeatherList[1].ValidTo.Should().Be(DateTime.Parse("2024-08-03T21:10:00Z"));
        
        _metarWeatherList[2].Airport.Should().Be("KSXU");
        _metarWeatherList[2].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _metarWeatherList[2].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T21:15:00Z"));
        _metarWeatherList[2].ValidTo.Should().Be(DateTime.Parse("2024-08-03T21:15:00Z"));
        
        _metarWeatherList[3].Airport.Should().Be("SBIZ");
        _metarWeatherList[3].WeatherLevel.Should().Be(WeatherCategory.VFR);
        _metarWeatherList[3].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T21:25:00Z"));
        _metarWeatherList[3].ValidTo.Should().Be(DateTime.Parse("2024-08-03T21:25:00Z"));
        
        _metarWeatherList[4].Airport.Should().Be("VISP");
        _metarWeatherList[4].WeatherLevel.Should().Be(WeatherCategory.MVFR);
        _metarWeatherList[4].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T21:30:00Z"));
        _metarWeatherList[4].ValidTo.Should().Be(DateTime.Parse("2024-08-03T21:30:00Z"));
        
        _metarWeatherList[5].Airport.Should().Be("VIUT");
        _metarWeatherList[5].WeatherLevel.Should().Be(WeatherCategory.MVFR);
        _metarWeatherList[5].ValidFrom.Should().Be(DateTime.Parse("2024-08-03T21:30:00Z"));
        _metarWeatherList[5].ValidTo.Should().Be(DateTime.Parse("2024-08-03T21:30:00Z"));
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