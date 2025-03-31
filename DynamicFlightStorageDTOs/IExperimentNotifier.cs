namespace DynamicFlightStorageDTOs
{
    public interface IExperimentNotifier
    {
        Task SendNotification(string title, string message);
        void SetNotificationEnabled(bool enabled);
        bool IsNotificationEnabled { get; }
    }
}
