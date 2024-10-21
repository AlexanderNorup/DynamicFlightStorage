using DynamicFlightStorageDTOs;
using System.Text.Json.Nodes;

namespace DynamicFlightStorageSimulation;

public static class WeatherCreator
{
    public static List<Weather> ReadWeatherJson(string jsonString)
    {
        var weatherList = new List<Weather>();
        
        JsonArray? tafs = JsonNode.Parse(jsonString)?.AsArray();
        if (tafs is null || tafs.Count < 1)
        {
            Console.WriteLine("The JSON does not contain a valid array.");
            return weatherList;
        }
        

        foreach (JsonNode? taf in tafs)
        {
            if (taf is not null)
            {
                weatherList.AddRange(HandleTaf(taf));
            }
        }
        
        return weatherList;
    }

    private static List<Weather> HandleTaf(JsonNode taf)
    {
        var weatherList = new List<Weather>();
        
        var tafText = taf["Text"]?.ToString();
        if (string.IsNullOrEmpty(tafText))
        {
            Console.WriteLine("TAF text is empty.");
            return weatherList;
        }
        
        var tafIdentifier = taf["Ident"]?.ToString();
        if (string.IsNullOrEmpty(tafIdentifier))
        {
            Console.WriteLine($"TAF identifier is empty for TAF: {tafIdentifier}");
            return weatherList;
        }
        
        
        
        if (!Enum.TryParse(taf["Conditions"]?[0]?["FlightRules"]?.ToString(), out WeatherCategory baseline))
        {
            Console.WriteLine($"Invalid weather category in TAF: {tafText}");
            return weatherList;
        }
        
        DateTime previousEnd  = DateTime.MaxValue;
        
        JsonArray? conditions = taf["Conditions"]?.AsArray();
        if (conditions is null || conditions.Count < 1)
        {
            return weatherList;
        }
        
        // Loop that goes through each time period and creates weather from that. 
        // For each condition, go from start to end and add the weather in that condition. 
        // After handling one condition, if end of condition is not equal to start of next condition,
        // create weather from end to start of next, with weather being baseline (first condition, or latest "becoming")

        // Maybe adjust loop such that the first iteration only really does the first line of the loop,
        // and then adds weather with baseline (as initial baseline is always the first condition
        foreach (var condition in conditions)
        {
            JsonNode? period = condition?["Period"];
            if (period is null)
            {
                continue;
            }
            
            (DateTime dateStart, DateTime dateEnd) = ParsePeriod(period, tafIdentifier);
            
            // If there is a gap in the "timeline" fill it with the current baseline weather category
            if (previousEnd < dateStart)
            {
                weatherList.Add(new Weather()
                {
                    ValidFrom = previousEnd,
                    ValidTo = dateStart,
                    Airport = tafIdentifier,
                    WeatherLevel = baseline
                });
                continue;
            }
            
            if (!Enum.TryParse(condition?["FlightRules"]?.ToString(), out WeatherCategory weatherCategory))
            {
                Console.WriteLine($"Invalid weather category in TAF: {tafText}");
                continue;
            }
            
            weatherList.Add(new Weather()
            {
                ValidFrom = dateStart,
                ValidTo = dateEnd,
                Airport = tafIdentifier,
                WeatherLevel = weatherCategory
            });
            
            var conditionChange = condition["Change"]?.ToString();

            if (conditionChange == "BECOMING")
            {
                baseline = weatherCategory;
            }

            previousEnd = dateEnd;

        }
        
        return weatherList;
    }

    private static (DateTime Start, DateTime End) ParsePeriod(JsonNode period, string identifier)
    {
        var dateStartString = period["DateStart"]?.ToString();
        var dateEndString = period["DateEnd"]?.ToString();
        
        if (dateEndString is null || dateStartString is null || dateEndString == "" || dateStartString == "")
        {
            throw new ArgumentException($"TAF does not contain a valid period. TAF: {identifier}");
        }

        DateTime dateStart = DateTime.MinValue;
        DateTime dateEnd = DateTime.MinValue;
        try
        {
            dateStart = DateTime.Parse(dateStartString);
            dateEnd = DateTime.Parse(dateEndString);
        }
        catch (FormatException e)
        {
            Console.WriteLine($"Parsing TAF date unsuccessful: {e.Message}");
        }

        return (dateStart, dateEnd);
    }
}