namespace DynamicFlightStorageDTOs
{
    public interface IRecalculateFlightEventPublisher
    {
        Task PublishRecalculationAsync(string flightIdentification);
    }
}
