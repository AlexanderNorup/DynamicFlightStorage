using DynamicFlightStorageDTOs;
using System.CommandLine;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace FlightRequestCleaner
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Can clean CalculateFlightPlan requests to anonymize them");

            var pathArgument = new Argument<string>("path", "Path to the directory with the XML files to clean");

            var outputOption = new Option<string>(
                ["--output", "-o"],
                () => AppContext.BaseDirectory,
                "Path to the folder where the cleaned files will be saved");

            rootCommand.AddArgument(pathArgument);
            rootCommand.AddOption(outputOption);

            rootCommand.SetHandler(CleanFlightRequests, pathArgument, outputOption);

            return await rootCommand.InvokeAsync(args);
        }

        public static void CleanFlightRequests(string path, string output)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"The path {path} is not a directory!");
                Environment.Exit(1);
            }
            Directory.CreateDirectory(output);

            foreach (var (reply, filePath) in RecursivelyFindFlights(path))
            {
                try
                {
                    var containingDirectory = Path.GetFileName(Path.GetDirectoryName(filePath));

                    var flight = FlightConverter.ConvertFlight(reply);

                    if (!DateTime.TryParseExact(containingDirectory, "yyyyMMdd HH-mm-ss-ffffff", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var plannedTime))
                    {
                        plannedTime = flight.ScheduledTimeOfDeparture.Subtract(TimeSpan.FromHours(5));
                    };
                    flight.DatePlanned = DateTime.SpecifyKind(plannedTime, DateTimeKind.Utc);

                    string fileOutPath = Path.Combine(output, GetFileName(flight) + ".json");
                    int i = 1;
                    while (File.Exists(fileOutPath))
                    {
                        fileOutPath = Path.Combine(output, GetFileName(flight) + $"_{i++}.json");
                        if (i > 100)
                        {
                            throw new InvalidOperationException("Too many files with the same name, aborting");
                        }
                    }
                    File.WriteAllText(fileOutPath, JsonSerializer.Serialize(flight));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Converted {filePath} => {fileOutPath}");
                    Console.ResetColor();
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to convert reply {filePath}: {e}");
                    Console.ResetColor();
                }
            }
        }

        private static IEnumerable<(XDocument reply, string path)> RecursivelyFindFlights(string path, int depth = 0)
        {
            if (depth > 100)
            {
                yield break;
            }

            //Find files in directory
            foreach (var file in Directory.EnumerateFiles(path))
            {
                if (file.Contains("reply_", StringComparison.OrdinalIgnoreCase) && file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    XDocument? reply = null;
                    try
                    {
                        reply = XDocument.Load(File.OpenRead(file));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error reading reply file {file}: {e.Message}");
                    }
                    if (reply is not null)
                    {
                        yield return (reply, file);
                    }
                }
            }

            foreach (var directory in Directory.EnumerateDirectories(path))
            {
                foreach (var result in RecursivelyFindFlights(directory, depth + 1))
                {
                    yield return result;
                }
            }
        }

        public static string GetFileName(Flight flight)
        {
            return $"flight{flight.ScheduledTimeOfDeparture:yyyyMMddTHHmmssff}";
        }
    }
}
