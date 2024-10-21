namespace DynamicFlightStorageDTOs
{
    public interface IRecalculateFlightEventPublisher
    {
        Task PublishRecalculationAsync(Flight flight);
    }
}
