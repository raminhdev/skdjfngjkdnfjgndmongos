using Utilities.Exceptions.Common;
using Utilities.Extensions;

namespace Utilities.Utilities
{
    public static class TimeHelper
    {
        public static GetStartAndEndOfMonthResult GetStartAndEndOfMonth(int year, int month)
        {
            try
            {
                DateTime start = new DateTime(year, month, 1, 0, 0, 0).AddHours(-8);
                DateTime end = new DateTime(year, month, GetContOfDaysOfMonth(year, month), 23, 59, 59).AddHours(-8);

                return new GetStartAndEndOfMonthResult
                {
                    StartDateTime = start,
                    EndDateTime = end,
                    StartTimeStamp = start.ToTimestamp(),
                    EndTimeStamp = end.ToTimestamp()
                };
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }

        public static string GetMonth(int monthNumber)
        {
            try
            {
                var months = new List<string>() { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
                return months[monthNumber - 1];
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }

        #region Private Methods
        private static int GetContOfDaysOfMonth(int year, int month)
        {
            try
            {
                var isLeapYear = Math.Ceiling(Convert.ToDecimal(year) / 4m) == Math.Floor(Convert.ToDecimal(year) / 4m) ? true : false;

                var daysCountOfMonth = new Dictionary<int, int>()
                {
                    {1,31 },
                    {2,isLeapYear ? 29 : 28 },
                    {3,31 },
                    {4,30 },
                    {5,31 },
                    {6,30 },
                    {7,31 },
                    {8,31 },
                    {9,30 },
                    {10,31 },
                    {11,30 },
                    {12,31 }
                };

                return daysCountOfMonth[month];
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }
        #endregion
    }

    public class GetStartAndEndOfMonthResult
    {
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public long StartTimeStamp { get; set; }
        public long EndTimeStamp { get; set; }
    }
}
