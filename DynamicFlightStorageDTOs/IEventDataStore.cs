﻿namespace DynamicFlightStorageDTOs
{
    public interface IEventDataStore
    {
        Task AddOrUpdateFlightAsync(Flight flight);
        Task DeleteFlightAsync(string id);
        Task AddWeatherAsync(Weather weather, DateTime recievedTime);
        Task StartAsync();
        Task ResetAsync();
    }
}
