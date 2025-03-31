using DynamicFlightStorageDTOs;
using Microsoft.Extensions.Options;

namespace DynamicFlightStorageUI
{
    public class PushoverExperimentNotifier : IExperimentNotifier
    {
        private IHttpClientFactory _httpClientFactory;
        private ILogger<PushoverExperimentNotifier> _logger;
        private PushoverOptions _options;
        public PushoverExperimentNotifier(IHttpClientFactory httpClientFactory, IOptions<PushoverOptions> options, ILogger<PushoverExperimentNotifier> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            IsEnabled = false; // Default to disabled
        }

        private bool IsEnabled { get; set; }

        public bool IsNotificationEnabled => IsEnabled;

        public async Task SendNotification(string title, string message)
        {
            if (!IsEnabled)
            {
                return;
            }

            _logger.LogInformation("Sending notification: {Title}: {Message}", title, message);

            // Send the notification
            using var client = _httpClientFactory.CreateClient("pushover");

            try
            {
                var payload = new PushoverNotification
                {
                    Title = title,
                    Message = message,
                    User = _options.UserId,
                    Token = _options.ApplicationToken
                };

                await client.PostAsJsonAsync("messages.json", payload).ConfigureAwait(false);
                _logger.LogInformation("Successfully sent notification.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification");
            }
        }

        public void SetNotificationEnabled(bool enabled)
        {
            _logger.LogInformation("Setting notification enabled to {Enabled}.", enabled);
            IsEnabled = enabled;
        }
    }
}
