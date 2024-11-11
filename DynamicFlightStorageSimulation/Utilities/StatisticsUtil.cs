namespace DynamicFlightStorageSimulation.Utilities
{
    public static class StatisticsUtil
    {
        // From https://stackoverflow.com/a/55467992
        public static double Median(this List<double> numbers)
        {
            if (numbers.Count == 0)
            {
                return 0;
            }

            numbers = numbers.OrderBy(n => n).ToList();

            var halfIndex = numbers.Count / 2;

            if (numbers.Count % 2 == 0)
            {
                return (numbers[halfIndex] + numbers[halfIndex - 1]) / 2.0;
            }

            return numbers[halfIndex];
        }

        // From https://stackoverflow.com/a/55467992
        public static double StdDev(this List<double> values)
        {
            if (values.Count <= 1)
            {
                return 0;
            }

            var avg = values.Average();
            var sum = values.Sum(d => Math.Pow(d - avg, 2));
            return Math.Sqrt(sum / (values.Count - 1));
        }
    }
}
