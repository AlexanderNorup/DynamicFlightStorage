using DynamicFlightStorageDTOs;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace FlightRequestCleaner
{
    internal class FlightConverter
    {
        internal static Flight ConvertFlight(XDocument reply)
        {
            var replyRoot = reply.FirstNode as XElement;
            if (replyRoot is not { Name.LocalName: "CalculateFlightPlanResponse" })
            {
                throw new InvalidDataException($"Invalid request or reply XML Types: {replyRoot?.Name?.LocalName}");
            }
            var replyFlightData = SelectElement(replyRoot, "/CalculateFlightPlanResponse/CalculateFlightPlanResult/FlightData");
            if (replyFlightData is null)
            {
                throw new InvalidDataException("Reply does not contain any valid flight data");
            }

            var departure = SelectElement(replyFlightData, "ATCData/DepartureAirport/ICAOCode")?.Value;
            var destination = SelectElement(replyFlightData, "ATCData/DestinationAirport/ICAOCode")?.Value;
            if (string.IsNullOrWhiteSpace(departure) || string.IsNullOrWhiteSpace(destination))
            {
                throw new InvalidDataException($"Departure ({departure}) or destination ({destination}) airport is empty");
            }

            var std = DateTime.Parse(SelectElement(replyFlightData, "FlightTimes/DepartureTimeUTC").Value).ToUniversalTime();
            var sta = DateTime.Parse(SelectElement(replyFlightData, "FlightTimes/EstimatedArrivalTime").Value).ToUniversalTime();

            var relatedAirports = new Dictionary<string, string>();


            if (SelectElement(replyFlightData, "CalcualtedAlternate1Route/DestinationAirport/ICAOCode")?.Value is { Length: > 0 } alt1)
            {
                relatedAirports.Add(alt1, "Alternate1");
            }
            if (SelectElement(replyFlightData, "CalcualtedAlternate2Route/DestinationAirport/ICAOCode")?.Value is { Length: > 0 } alt2)
            {
                relatedAirports.Add(alt2, "Alternate2");
            }
            if (SelectElement(replyFlightData, "CalculatedTakeOffAlternateRoute/DestinationAirport/ICAOCode")?.Value is { Length: > 0 } takeOffAlt)
            {
                relatedAirports.Add(takeOffAlt, "TakeOffAlt");
            }
            if (SelectElement(replyFlightData, "CalculatedIapEraRoute/DestinationAirport/ICAOCode")?.Value is { Length: > 0 } iapEra)
            {
                relatedAirports.Add(iapEra, "IapEra");
            }
            if (SelectElement(replyFlightData, "CalculatedSecondDestinationRoute/DestinationAirport/ICAOCode")?.Value is { Length: > 0 } secondDest)
            {
                relatedAirports.Add(secondDest, "SecondDest");
            }
            if (SelectElement(replyFlightData, "CalculatedSecondDestinationAlternateRoute/DestinationAirport/ICAOCode")?.Value is { Length: > 0 } secondDestAlt)
            {
                relatedAirports.Add(secondDestAlt, "SecondDestAlt");
            }

            // Adequates
            if (SelectElement(replyFlightData, "AdequateAirports") is { } adequateAirports)
            {
                foreach (var adequateAirport in adequateAirports.Nodes())
                {
                    var icao = SelectElement(adequateAirport as XElement, "ICAOCode")?.Value;
                    if (!string.IsNullOrWhiteSpace(icao) && !relatedAirports.ContainsKey(icao))
                    {
                        relatedAirports.Add(icao, "AdequateAirport");
                    }
                }
            }

            // ETP Airports
            if (SelectElement(replyFlightData, "ETPInformation/ETPAirports") is { } etpAirports)
            {
                foreach (var etpAirport in etpAirports.Nodes())
                {
                    var icao = SelectElement(etpAirport as XElement, "ICAOCode")?.Value;
                    if (!string.IsNullOrWhiteSpace(icao) && !relatedAirports.ContainsKey(icao))
                    {
                        relatedAirports.Add(icao, "ETPAirport");
                    }
                }
            }

            var route = new List<RouteNode>();
            if (SelectElement(replyFlightData, "PathwayData/DestinationData") is { } destPath)
            {
                foreach (var pathItem in destPath.Nodes())
                {
                    var pathElement = pathItem as XElement;
                    if (pathElement is null)
                    {
                        continue;
                    }
                    var point = SelectElement(pathElement, "WaypointData/WaypointId")?.Value;
                    if (string.IsNullOrWhiteSpace(point))
                    {
                        continue;
                    }

                    var pointType = SelectElement(pathElement, "WaypointData/FixType")?.Value;

                    var lat = double.Parse(SelectElement(pathElement, "WaypointData/Latitude")?.Value ?? "0", CultureInfo.InvariantCulture);
                    var lon = double.Parse(SelectElement(pathElement, "WaypointData/Longitude")?.Value ?? "0", CultureInfo.InvariantCulture);

                    var flightLevel = int.Parse(SelectElement(pathElement, "FlightData/FlightLevel")?.Value ?? "0");

                    var timeOverWaypointStr = SelectElement(pathElement, "FlightData/TimeOverWaypoint")?.Value;
                    if (string.IsNullOrWhiteSpace(timeOverWaypointStr))
                    {
                        continue;
                    }
                    var timeOverWaypoint = DateTime.Parse(timeOverWaypointStr).ToUniversalTime();

                    route.Add(new RouteNode()
                    {
                        PointIdentifier = point,
                        PointType = pointType,
                        Lattitude = lat,
                        Longitude = lon,
                        FlightLevel = flightLevel,
                        TimeOverWaypoint = timeOverWaypoint
                    });
                }
            }

            var flight = new Flight()
            {
                FlightIdentification = Guid.NewGuid().ToString(), // While there is a real flight identification on the request, we anonymize it by making a new unique one. 
                DepartureAirport = departure,
                DestinationAirport = destination,
                OtherRelatedAirports = relatedAirports,
                ScheduledTimeOfDeparture = std,
                ScheduledTimeOfArrival = sta,
                Route = route
            };
            return flight;
        }

        // From: https://stackoverflow.com/a/69946949
        private static XElement SelectElement(XElement startElement, string xpathExpression, XmlNamespaceManager namespaceManager = null)
        {
            // XPath 1.0 does not have support for default namespace, so we have to expand our path.
            if (namespaceManager == null)
            {
                var reader = startElement.CreateReader();
                namespaceManager = new XmlNamespaceManager(reader.NameTable);
            }
            var defaultNamespace = startElement.GetDefaultNamespace();
            var defaultPrefix = namespaceManager.LookupPrefix(defaultNamespace.NamespaceName);
            if (string.IsNullOrEmpty(defaultPrefix))
            {
                defaultPrefix = "ᆞ";
                namespaceManager.AddNamespace(defaultPrefix, defaultNamespace.NamespaceName);
            }
            xpathExpression = AddPrefix(xpathExpression, defaultPrefix);
            var selected = startElement.XPathSelectElement(xpathExpression, namespaceManager);
            return selected;
        }

        private static string AddPrefix(string xpathExpression, string prefix)
        {
            // Implementation notes:
            // * not perfect, but it works for our use case.
            // * supports: "Name~~" "~~/Name~~" "~~@Name~~" "~~[Name~~" "~~[@Name~~"
            // * does not work in complex expressions like //*[local-name()="HelloWorldResult" and namespace-uri()='http://tempuri.org/']/text()
            // * does not exclude strings like 'string' or function like func()
            var s = Regex.Replace(xpathExpression, @"(?<a>/|\[@|@|\[|^)(?<name>\w(\w|[-])*)", "${a}${prefix}:${name}".Replace("${prefix}", prefix));
            return s;
        }
    }
}
