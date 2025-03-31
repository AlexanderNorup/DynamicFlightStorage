using System.ComponentModel.DataAnnotations;

namespace DynamicFlightStorageUI
{
    public class PushoverOptions
    {
        [Required]
        public required string UserId { get; init; }

        [Required]
        public required string ApplicationToken { get; init; }

        [Required]
        public required string Endpoint { get; init; }

        public bool EnableNotiticationOnBoot { get; init; }
    }
}
