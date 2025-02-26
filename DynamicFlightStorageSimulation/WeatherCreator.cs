using DynamicFlightStorageDTOs;
using System.Text.Json.Nodes;

namespace DynamicFlightStorageSimulation;

public static class WeatherCreator
{
    public static List<Weather> ReadWeatherJson(string jsonString)
    {
        var weatherList = new List<Weather>();

        JsonArray? weather = JsonNode.Parse(jsonString)?.AsArray();
        if (weather is null || weather.Count < 1)
        {
            Console.WriteLine("The JSON does not contain a valid array.");
            return weatherList;
        }


        foreach (JsonNode? taf in weather)
        {
            if (taf is not null)
            {
                weatherList.AddRange(HandleJsonWeather(taf));
            }
        }

        return weatherList;
    }

    private static List<Weather> HandleJsonWeather(JsonNode data)
    {
        var weatherList = new List<Weather>();

        var identifier = data["ID"]?.ToString();
        if (string.IsNullOrEmpty(identifier))
        {
            //Console.WriteLine($"Identifier is empty for {fullText}");
            return weatherList;
        }

        // Check for airport identifier
        var airportId = data["Ident"]?.ToString();
        if (string.IsNullOrEmpty(airportId))
        {
            //Console.WriteLine($"Airport identifier is empty for {identifier}");
            return weatherList;
        }

        var conditions = data["Conditions"]?.AsArray();

        // Handle metar
        if (conditions is null)
        {
            if (!Enum.TryParse(data["FlightRules"]?.ToString(), out WeatherCategory category))
            {
                //Console.WriteLine($"Invalid weather category in METAR {identifier}");
                return weatherList;
            }

            // Check for valid period
            DateTime dateIssued;
            try
            {
                dateIssued = ParseDate(data["DateIssued"], identifier);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Invalid date in METAR {identifier}: {e.Message}");
                return weatherList;
            }

            weatherList.Add(new Weather()
            {
                Id = identifier,
                ValidFrom = dateIssued,
                ValidTo = dateIssued.AddHours(1),
                Airport = airportId,
                WeatherLevel = category,
                DateIssued = dateIssued
            });
            return weatherList;
        }

        // Handle taf
        if (conditions.Count > 1)
        {
            // Check for valid period
            DateTime dateIssued;
            try
            {
                dateIssued = ParseDate(data["DateIssued"], identifier);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Invalid date in METAR {identifier}: {e.Message}");
                return weatherList;
            }

            // Check for valid period
            JsonNode? validPeriod = data["Period"];
            if (validPeriod is null)
            {
                //Console.WriteLine($"Period does not exist for TAF {identifier}");
                return weatherList;
            }

            DateTime validTo;
            try
            {
                validTo = ParseDate(validPeriod["DateEnd"], identifier);
            }
            catch (Exception)
            {
                return weatherList;
            }

            return HandleTaf(conditions, identifier, validTo, airportId, dateIssued);
        }
        return weatherList;
    }

    private static List<Weather> HandleTaf(JsonArray conditions, string identifier, DateTime validTo, string airportId, DateTime dateIssued)
    {
        var weatherList = new List<Weather>();

        if (!Enum.TryParse(conditions[0]?["FlightRules"]?.ToString(), out WeatherCategory baseline))
        {
            //Console.WriteLine($"Invalid weather category in TAF {identifier}");
            return weatherList;
        }

        DateTime previousEnd = DateTime.MaxValue;

        // Loop that goes through each time period and creates weather from that. 
        // For each condition, go from start to end and add the weather in that condition. 
        // After handling one condition, if end of condition is not equal to start of next condition,
        // create weather from end to start of next, with weather being baseline (first condition, or latest "becoming")

        // Maybe adjust loop such that the first iteration only really does the first line of the loop,
        // and then adds weather with baseline (as initial baseline is always the first condition
        var idIterator = 0;
        foreach (var condition in conditions)
        {
            JsonNode? period = condition?["Period"];
            if (period is null)
            {
                Console.WriteLine($"No period in TAF {identifier}");
                continue;
            }

            DateTime dateStart, dateEnd;
            try
            {
                dateStart = ParseDate(period["DateStart"], identifier);
                dateEnd = ParseDate(period["DateEnd"], identifier);
            }
            catch (Exception)
            {
                continue;
            }

            if (!Enum.TryParse(condition?["FlightRules"]?.ToString(), out WeatherCategory weatherCategory))
            {
                //Console.WriteLine($"Invalid weather category in TAF {identifier}");
                continue;
            }



            // If there is a gap in the "timeline" fill it with the current baseline weather category
            if (previousEnd < dateStart)
            {
                weatherList.Add(new Weather()
                {
                    Id = identifier + "_" + idIterator++,
                    ValidFrom = previousEnd,
                    ValidTo = dateStart,
                    Airport = airportId,
                    WeatherLevel = baseline,
                    DateIssued = dateIssued,
                });

            }

            weatherList.Add(new Weather()
            {
                Id = identifier + "_" + idIterator++,
                ValidFrom = dateStart,
                ValidTo = dateEnd,
                Airport = airportId,
                WeatherLevel = weatherCategory,
                DateIssued = dateIssued,
            });

            var conditionChange = condition["Change"]?.ToString();

            if (conditionChange == "BECOMING")
            {
                baseline = weatherCategory;
            }

            previousEnd = dateEnd;

        }

        if (previousEnd < validTo)
        {
            weatherList.Add(new Weather()
            {
                Id = identifier + "_" + idIterator,
                ValidFrom = previousEnd,
                ValidTo = validTo,
                Airport = airportId,
                WeatherLevel = baseline,
                DateIssued = dateIssued,
            });
        }

        return weatherList;
    }


    private static DateTime ParseDate(JsonNode? date, string identifier)
    {
        var dateString = date?.ToString();
        if (string.IsNullOrEmpty(dateString))
        {
            throw new ArgumentException($"Invalid date for {identifier}");
        }

        return DateTime.Parse(dateString);
    }
}