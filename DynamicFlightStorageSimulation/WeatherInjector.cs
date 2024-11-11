using DynamicFlightStorageDTOs;
using Microsoft.Extensions.Logging;

namespace DynamicFlightStorageSimulation;

public class WeatherInjector
{
    private readonly SimulationEventBus _eventBus;
    private Queue<string> _metarFiles;
    private Queue<string> _tafFiles;
    private Queue<Weather>? _taf;
    private Queue<Weather>? _metar;
    private int _counter;
    public WeatherInjector(SimulationEventBus eventBus, string metarPath, string tafPath)
    {
        _eventBus = eventBus;
        if (!Directory.Exists(metarPath))
        {
            Console.WriteLine($"The path {metarPath} is not a directory!");
            Environment.Exit(1);
        }
        if (!Directory.Exists(tafPath))
        {
            Console.WriteLine($"The path {tafPath} is not a directory!");
            Environment.Exit(1);
        }
        _metarFiles = new(FindFiles(metarPath));
        _tafFiles = new(FindFiles(tafPath));
    }

    public async Task PublishWeatherUntil(DateTime date, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        const int MaxWeatherBatch = 500;
        var weatherBatches = GetWeatherUntill(date, cancellationToken).Chunk(MaxWeatherBatch).ToList();

        if (weatherBatches.Count == 0)
        {
            //logger?.LogDebug("No weather data to publish (untill {Untill}).", date);
            return;
        }

        logger?.LogInformation("Publishing {BatchNum} weather batches (until {Untill}).",
            weatherBatches.Count,
            date);
        int batchCount = 0;
        foreach (var weatherBatch in weatherBatches)
        {
            await _eventBus.PublishWeatherAsync(weatherBatch).ConfigureAwait(false);
            if (++batchCount % 10 == 0)
            {
                logger?.LogInformation("Published {Count}/{Total} weather batches ({Untill}).",
                    batchCount,
                    weatherBatches.Count,
                    date);
            }
        }
    }

    public IEnumerable<Weather> GetWeatherUntill(DateTime date, CancellationToken cancellationToken = default)
    {
        DateTime currentDate;
        if (_metar is null || _metar.Count < 1)
        {
            _metar = ReadNextFile(_metarFiles, "metar");
        }

        // There might not be any METAR weather left, but we don't want to return yet as there might still be TAF
        if (_metar.Count > 0)
        {
            currentDate = _metar.Peek().ValidTo;

            while (currentDate < date && _metar.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                currentDate = _metar.Peek().ValidTo;
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
        currentDate = _taf.Peek().ValidTo;

        while (currentDate < date && _taf.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            currentDate = _taf.Peek().ValidTo;
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