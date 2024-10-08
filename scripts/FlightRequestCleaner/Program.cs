﻿using System.CommandLine;
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
                    var flight = FlightConverter.ConvertFlight(reply);
                    string fileOutPath = Path.Combine(output, MD5Hash(filePath) + ".json");
                    File.WriteAllText(fileOutPath, JsonSerializer.Serialize(flight));

                    Console.WriteLine($"Converted {filePath} => {fileOutPath}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to convert reply {filePath}: {e}");
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

        public static string MD5Hash(string input)
        {
            // Use input string to calculate MD5 hash
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return Convert.ToHexString(hashBytes);
            }
        }
    }
}
