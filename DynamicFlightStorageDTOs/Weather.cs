using System.Diagnostics;

namespace DynamicFlightStorageDTOs
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class Weather
    {
        public required string Airport { get; set; }
        /// <summary>
        /// Weather categories. Lowest is best (VFR), highest is worst (MIFR).
        /// </summary>
        public WeatherCategory WeatherLevel { get; set; }
        public DateTime ValidFrom { get; set; }
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