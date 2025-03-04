namespace DynamicFlightStorageDTOs
{
    public interface IWeatherService
    {
        Weather GetWeather(string airport, DateTime dateTime);
        void ResetWeatherService();
    }
}
