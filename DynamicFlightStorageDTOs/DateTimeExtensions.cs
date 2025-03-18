namespace DynamicFlightStorageDTOs
{
    public static class DateTimeExtensions
    {
        public static int ToUnixTimeSeconds(this DateTime dateTime)
        {
            return (int)((DateTimeOffset)dateTime).ToUnixTimeSeconds();
        }
    }
}
