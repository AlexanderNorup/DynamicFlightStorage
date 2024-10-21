
namespace DynamicFlightStorageDTOs
{
    /// <summary>
    /// Lowest is best (VFR), highest is worst (MIFR)
    /// </summary>
    public enum WeatherCategory
    {
        VFR = 0,
        MVFR = 1,
        IFR = 2,
        LIFR = 3,
        Undefined = -999 // Treated as best weather imagineable so it will be recalculated next time regardless to get a proper weather status
    }
}
