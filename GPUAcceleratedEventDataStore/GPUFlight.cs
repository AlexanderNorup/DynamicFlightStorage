using DynamicFlightStorageDTOs;

namespace GPUAcceleratedEventDataStore
{
    internal record GPUFlight(Flight Flight, int InternalId, WeatherCategory lastSeenWeather);
}
