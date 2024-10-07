namespace DynamicFlightStorageDTOs
{
    public class Weather
    {
        public required string Airport { get; set; }
        /// <summary>
        /// Weather categories. Lowest is best (VFR), highest is worst (MIFR).
        /// </summary>
        public WeatherCategory WeatherLevel { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
    }
}