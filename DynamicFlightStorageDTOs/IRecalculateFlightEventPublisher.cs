namespace DynamicFlightStorageDTOs
{
    public interface IRecalculateFlightEventPublisher
    {
        Task PublishRecalculationAsync(params Flight[] flight);
    }
}
