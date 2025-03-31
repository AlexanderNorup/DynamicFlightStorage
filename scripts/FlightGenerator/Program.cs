using DynamicFlightStorageDTOs;
using System.Text.Json;
using System.CommandLine;
using MessagePack;

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

            var compressFlightsCommand = new Command("compress", "Compress Flights so they're easier to load and deserialize");
            var compressInputArgument = new Argument<string>("path", "Path to folder containing the files to compress");
            var compressOutputArgument = new Argument<string>("output", "Path to where the output .bin file");
            compressFlightsCommand.AddArgument(compressInputArgument);
            compressFlightsCommand.AddArgument(compressOutputArgument);
            compressFlightsCommand.SetHandler(CompressFlights, compressInputArgument, compressOutputArgument);
            rootCommand.AddCommand(compressFlightsCommand);

            rootCommand.SetHandler(LaunchGeneration, outputOption);

            return await rootCommand.InvokeAsync(args);
        }

        static void LaunchGeneration(string outputDirPath)
        {
            var airportJsonPath = Path.Combine("Resources", "Airports.json");

            var airports = JsonSerializer.Deserialize<Airport[]>(File.ReadAllText(airportJsonPath)) ?? throw new InvalidDataException($"{airportJsonPath} could not be deserialized");

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

        public static readonly MessagePackSerializerOptions MessagePackOptions = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
        static void CompressFlights(string inputPath, string outputPath)
        {
            if (!Directory.Exists(inputPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"The path {inputPath} does not exist");
                Console.ResetColor();
                return;
            }
            var files = Directory.GetFiles(inputPath, "*.json");
            Console.WriteLine("Found {0} files to compress", files.Length);
            var flights = new List<Flight>();
            foreach (var file in files)
            {
                flights.Add(JsonSerializer.Deserialize<Flight>(File.ReadAllText(file)) ?? throw new InvalidOperationException($"Could not deserialize {file}"));
                if (flights.Count % 10_000 == 0)
                {
                    Console.WriteLine($"Deserialized {flights.Count}/{files.Length} ({flights.Count / (double)files.Length * 100d:.00}%) flights");
                }
            }

            Console.WriteLine("Loaded {0} flights", files.Length);
            var compressedData = MessagePackSerializer.Serialize(flights, MessagePackOptions);
            Console.WriteLine("Serialized the flights using message-pack", files.Length);

            File.WriteAllBytes(outputPath, compressedData);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Compressed {files.Length} files to {outputPath} => {Path.GetFullPath(outputPath)}");
            Console.ResetColor();
        }
    }
}
