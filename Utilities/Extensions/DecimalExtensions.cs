namespace Utilities.Extensions
{
    public static class DecimalExtensions
    {
        public static decimal ToFixed(this decimal number, int precision = 8)
        {
            return precision == 0
                ? Math.Truncate(number)
                : Math.Truncate(number * (decimal)Math.Pow(10, precision)) / (decimal)Math.Pow(10, precision);
        }
    }
}
