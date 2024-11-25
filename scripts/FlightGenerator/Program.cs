using DynamicFlightStorageDTOs;
using System.Text.Json;
using System.CommandLine;

namespace FlightGenerator
{
    internal class Program
    {
        private const int MaxFlightLengthHours = 10;
        private const int MinFlightLengthHours = 1;
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Can generate flights");

            var outputOption = new Option<string>(
                ["--output", "-o"],
                () => AppContext.BaseDirectory,
                "Path to the folder where the flights will be saved");

            rootCommand.AddOption(outputOption);

            rootCommand.SetHandler(LaunchGeneration, outputOption);

            return await rootCommand.InvokeAsync(args);
        }

        static void LaunchGeneration(string outputDirPath)
        {
            var airportJsonPath = Path.Combine("Resources", "Airports.json");
            
            var airports = JsonSerializer.Deserialize<Airport[]>(File.ReadAllText(airportJsonPath));
            
            var dateStart = DateTime.Parse("2024-10-11T00:00:00Z");
            var dateEnd = DateTime.Parse("2024-10-11T23:00:00Z");

            var flights = GenerateRandomFlights(4000, airports, dateStart, dateEnd);
            
            WriteFlights(flights, outputDirPath);
        }

        static Flight[] GenerateRandomFlights(int count, Airport[] airports, DateTime start, DateTime end)
        {
            Random random = new Random();
            
            var flights = new Flight[count];

            for (int i = 0; i < flights.Length; i++)
            {
                var startDate = GetRandomDate(start, end);
                var flightLength = random.NextDouble() * (MaxFlightLengthHours - MinFlightLengthHours) + MinFlightLengthHours;
                var endDate = startDate.AddHours(flightLength);
                
                flights[i] = new Flight()
                {
                    FlightIdentification = Guid.NewGuid().ToString(),
                    DepartureAirport = airports[random.Next(airports.Length)].ICAO,
                    DestinationAirport = airports[random.Next(airports.Length)].ICAO,
                    DatePlanned = startDate.AddDays(-7),
                    ScheduledTimeOfDeparture = startDate,
                    ScheduledTimeOfArrival = endDate
                };
            }
            
            return flights;
        }

        static void WriteFlights(Flight[] flights, string outputDir)
        {
            foreach (var flight in flights)
            {
                string fileOutPath = Path.Combine(outputDir, GetFileName(flight) + ".json");
                int i = 1;
                while (File.Exists(fileOutPath))
                {
                    fileOutPath = Path.Combine(outputDir, GetFileName(flight) + $"_{i++}.json");
                    if (i > 100)
                    {
                        throw new InvalidOperationException("Too many files with the same name, aborting");
                    }
                }
                File.WriteAllText(fileOutPath, JsonSerializer.Serialize(flight));
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Generated {fileOutPath}");
                Console.ResetColor();
            }
            
            
        }

        static DateTime GetRandomDate(DateTime startDate, DateTime endDate)
        {
            Random random = new Random();
            long rangeTicks = endDate.Ticks - startDate.Ticks;
            long randomTicks = (long)(random.NextDouble() * rangeTicks);
            DateTime randomDate = new DateTime(startDate.Ticks + randomTicks);
            return randomDate;
        }
        
        static string GetFileName(Flight flight)
        {
            return $"flight{flight.DatePlanned:yyyyMMddTHHmmssff}";
        }
    }
}
