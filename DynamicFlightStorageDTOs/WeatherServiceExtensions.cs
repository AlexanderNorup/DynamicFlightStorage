namespace DynamicFlightStorageDTOs
{
    public static class WeatherServiceExtensions
    {
        public static Dictionary<string, WeatherCategory> GetWeatherCategoriesForFlight(this IWeatherService weatherService, Flight flight)
        {
            var weather = new Dictionary<string, WeatherCategory>()
            {
                { flight.DepartureAirport, weatherService.GetWeather(flight.DepartureAirport, flight.ScheduledTimeOfDeparture).WeatherLevel }
            };

            if (flight.DepartureAirport != flight.DestinationAirport)
            {
                weather.Add(flight.DestinationAirport, weatherService.GetWeather(flight.DestinationAirport, flight.ScheduledTimeOfArrival).WeatherLevel);
            }

            var timeMiddleOfFlight = flight.ScheduledTimeOfDeparture + ((flight.ScheduledTimeOfArrival - flight.ScheduledTimeOfDeparture) / 2);
            foreach (var airport in flight.OtherRelatedAirports.Keys.Where(x => !weather.ContainsKey(x)).Distinct())
            {
                weather.Add(airport, weatherService.GetWeather(airport, timeMiddleOfFlight).WeatherLevel);
            }
            return weather;
        }
    }
}
