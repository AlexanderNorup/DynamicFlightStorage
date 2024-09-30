namespace DynamicFlightStorageDTOs
{
    public interface IFlightDataStorage
    {
        public Task AddFlightAsync(IFlight flight);
        public Task RemoveFlightAsync(IFlight flight);

        public Task AddWeatherAsync();
    }
}
