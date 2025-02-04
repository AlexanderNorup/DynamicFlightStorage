using DynamicFlightStorageDTOs;
using Microsoft.Extensions.Logging;

namespace DynamicFlightStorageSimulation;

public class WeatherInjector
{
    private readonly SimulationEventBus _eventBus;
    private Queue<string> _metarFiles = new();
    private Queue<string> _tafFiles = new();
    private Queue<Weather>? _taf;
    private Queue<Weather>? _metar;

    private string _metarPath = "metar";
    private string _tafPath = "metar";
    public WeatherInjector(SimulationEventBus eventBus, string metarPath, string tafPath)
    {
        _eventBus = eventBus;
        if (!Directory.Exists(metarPath))
        {
            throw new ArgumentException($"The metar-path {metarPath} is not a directory");
        }
        if (!Directory.Exists(tafPath))
        {
            throw new ArgumentException($"The taf-path {tafPath} is not a directory");
        }
        _metarPath = metarPath;
        _tafPath = tafPath;
        ResetReader();
    }

    public void ResetReader()
    {
        _metar = null;
        _taf = null;
        _metarFiles = new(FindFiles(_metarPath));
        _tafFiles = new(FindFiles(_tafPath));
    }

    public async Task PublishWeatherUntil(DateTime date, string experimentId, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        var weatherBatches = GetWeatherUntil(date, cancellationToken).ToList();

        if (weatherBatches.Count == 0)
        {
            //logger?.LogDebug("No weather data to publish (until {Until}).", date);
            return;
        }

        logger?.LogDebug("Publishing {Count} weather batches (until {Until}).",
            weatherBatches.Count,
            date);
        int weatherCount = 0;
        foreach (var weatherBatch in weatherBatches)
        {
            await _eventBus.PublishWeatherAsync(weatherBatch, experimentId).ConfigureAwait(false);
            if (++weatherCount % 10_000 == 0)
            {
                logger?.LogDebug("Published {Count}/{Total} weather batches (until {Until}).",
                    weatherCount,
                    weatherBatches.Count,
                    date);
            }
        }
    }

    public IEnumerable<Weather> GetWeatherUntil(DateTime date, CancellationToken cancellationToken = default)
    {
        DateTime currentDate;
        if (_metar is null || _metar.Count < 1)
        {
            _metar = ReadNextFile(_metarFiles, "metar");
        }

        // There might not be any METAR weather left, but we don't want to return yet as there might still be TAF
        if (_metar.Count > 0)
        {
            currentDate = _metar.Peek().DateIssued;

            while (currentDate < date && _metar.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                currentDate = _metar.Peek().DateIssued;
                yield return _metar.Dequeue();

                // If no more METAR then refill. If an empty queue is returned, the loop ends
                if (_metar is null || _metar.Count < 1)
                {
                    _metar = ReadNextFile(_metarFiles, "metar");
                }
            }
        }

        if (_taf is null || _taf.Count < 1)
        {
            _taf = ReadNextFile(_tafFiles, "taf");
            if (_taf.Count < 1) { yield break; }
        }
        currentDate = _taf.Peek().DateIssued;

        while (currentDate < date && _taf.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            currentDate = _taf.Peek().DateIssued;
            yield return _taf.Dequeue();

            // If no more TAF then refill. If an empty queue is returned, the loop ends
            if (_taf is null || _taf.Count < 1)
            {
                _taf = ReadNextFile(_tafFiles, "taf");
            }
        }
    }

    private List<string> FindFiles(string path)
    {
        var files = new List<string>();
        foreach (var file in Directory.EnumerateFiles(path))
        {
            if (!((file.Contains("metar", StringComparison.OrdinalIgnoreCase)
                   || file.Contains("taf", StringComparison.OrdinalIgnoreCase))
                  && file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"Files {file} does not fit");
                continue;
            }
            files.Add(file);
        }
        files.Sort();
        return files;
    }

    private Queue<Weather> ReadNextFile(Queue<string> fileQueue, string type)
    {
        // If no more files
        if (fileQueue.Count == 0)
        {
            Console.WriteLine($"Out of {type} files.");
            return new();
        }
        return new Queue<Weather>(WeatherCreator.ReadWeatherJson(File.ReadAllText(fileQueue.Dequeue())));
    }

    public Queue<string> GetMetarFiles()
    {
        return _metarFiles;
    }

    public Queue<string> GetTafFiles()
    {
        return _tafFiles;
    }
}