using System.Globalization;
using System.Runtime.InteropServices;

namespace Utilities.Extensions
{
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Convert DateTime to Unix Timestamp (seconds)
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static long ToTimestamp(this DateTime dateTime)
        {
            return new DateTimeOffset(dateTime.ToUniversalTime()).ToUnixTimeSeconds();
        }

        /// <summary>
        /// Convert DateTime to Unix Timestamp (milliseconds)
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static long ToTimestampMilliseconds(this DateTime dateTime)
        {
            return new DateTimeOffset(dateTime.ToUniversalTime()).ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Convert Unix Timestamp (seconds) to DateTime (UTC)
        /// </summary>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        public static DateTime ToDateTime(this long timestamp)
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
        }

        /// <summary>
        /// Convert Unix Timestamp (milliseconds) to DateTime (UTC)
        /// </summary>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        public static DateTime ToDateTimeMilliseconds(this long timestamp)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
        }

        public static string ToJalali(this DateTime dateTime, bool onlyDate = false)
        {
            try
            {
                TimeZoneInfo cstZone = null;

                var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
                var isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
                if (isLinux || isMac) cstZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
                else cstZone = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");

                var cstTime = TimeZoneInfo.ConvertTimeFromUtc(dateTime, cstZone);
                var pc = new PersianCalendar();
                return onlyDate
                    ? $"{pc.GetYear(cstTime).ToString().PadLeft(4, '0')}/{pc.GetMonth(cstTime).ToString().PadLeft(2, '0')}/{pc.GetDayOfMonth(cstTime).ToString().PadLeft(2, '0')}"
                    : $"{pc.GetYear(cstTime).ToString().PadLeft(4, '0')}/{pc.GetMonth(cstTime).ToString().PadLeft(2, '0')}/{pc.GetDayOfMonth(cstTime).ToString().PadLeft(2, '0')}-{pc.GetHour(cstTime)}:{pc.GetMinute(cstTime)}:{pc.GetSecond(cstTime)}";
            }
            catch (InvalidTimeZoneException)
            {
                return null;
            }
        }
    }
}