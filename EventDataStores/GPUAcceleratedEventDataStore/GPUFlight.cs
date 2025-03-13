using DynamicFlightStorageDTOs;

namespace GPUAcceleratedEventDataStore
{
    internal record GPUFlight(Flight Flight, int InternalId, Dictionary<string, WeatherCategory> Weather);
}
