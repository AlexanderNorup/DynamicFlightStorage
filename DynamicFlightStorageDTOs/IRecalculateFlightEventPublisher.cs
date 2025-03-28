namespace DynamicFlightStorageDTOs
{
    public interface IRecalculateFlightEventPublisher
    {
        Task PublishRecalculationAsync(string flightIdentification, string weatherIdentification, TimeSpan lag);
    }
}
