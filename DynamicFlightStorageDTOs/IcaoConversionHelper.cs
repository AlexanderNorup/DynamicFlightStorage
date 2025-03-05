namespace DynamicFlightStorageDTOs;

public static class IcaoConversionHelper
{
    public static int ConvertIcaoToInt(string icao)
    {
        if (icao.Length > 4)
            throw new ArgumentException($"ICAO code must be at most 4 characters long. Given: {icao}", nameof(icao));
        icao = icao.PadRight(4, ' ');
        return (icao[0] << 24) | (icao[1] << 16) | (icao[2] << 8) | icao[3];
    }
}