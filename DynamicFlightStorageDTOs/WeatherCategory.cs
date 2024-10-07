
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
        MIFR = 3,
        Undefined = 999
    }
}
