using MessagePack;
using System.Diagnostics;

namespace DynamicFlightStorageDTOs
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    [MessagePackObject]
    public class Weather
    {
        [Key(0)]
        public required string Id { get; set; }
        
        [Key(1)]
        public required string Airport { get; set; }

        /// <summary>
        /// Weather categories. Lowest is best (VFR), highest is worst (MIFR).
        /// </summary>
        [Key(2)]
        public WeatherCategory WeatherLevel { get; set; }
        [Key(3)]
        public DateTime ValidFrom { get; set; }
        [Key(4)]
        public DateTime ValidTo { get; set; }

        public override string ToString()
        {
            return $"Weather for {Airport}: {WeatherLevel} @ {ValidFrom}-{ValidTo}";
        }

        private string GetDebuggerDisplay()
        {
            return ToString();
        }
    }
}