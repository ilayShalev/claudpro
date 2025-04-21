using System;
using System.Globalization;

namespace claudpro.Utilities
{
    /// <summary>
    /// Provides standardized time formatting and parsing across the application
    /// </summary>
    public static class TimeFormatUtility
    {
        // Format used for internal storage of time values
        private const string InternalTimeFormat = "HH:mm";

        // Format used for display in UI
        private const string DisplayTimeFormat = "HH:mm";

        /// <summary>
        /// Formats a time string consistently for display
        /// </summary>
        /// <param name="timeString">The time string to format</param>
        /// <returns>Formatted time string for display</returns>
        public static string FormatTimeDisplay(string timeString)
        {
            if (string.IsNullOrEmpty(timeString))
                return "Not scheduled";

            // Try to parse the time
            if (ParseToDateTime(timeString, out DateTime parsedTime))
            {
                // Return in display format
                return parsedTime.ToString(DisplayTimeFormat);
            }

            // Return the original if parsing fails
            return timeString;
        }

        /// <summary>
        /// Formats a DateTime as a standardized time string for storage
        /// </summary>
        /// <param name="dateTime">DateTime to format</param>
        /// <returns>Formatted time string for storage</returns>
        public static string FormatTimeStorage(DateTime dateTime)
        {
            return dateTime.ToString(InternalTimeFormat);
        }

        /// <summary>
        /// Attempts to parse a time string to DateTime, handling various formats
        /// </summary>
        /// <param name="timeString">The time string to parse</param>
        /// <param name="result">The parsed DateTime if successful</param>
        /// <returns>True if parsing was successful, false otherwise</returns>
        public static bool ParseToDateTime(string timeString, out DateTime result)
        {
            result = DateTime.MinValue;

            if (string.IsNullOrEmpty(timeString))
                return false;

            // Try standard DateTime parsing first
            if (DateTime.TryParse(timeString, out result))
                return true;

            // Try parsing with just the time format
            if (DateTime.TryParseExact(timeString,
                new[] { "h:mm tt", "hh:mm tt", "H:mm", "HH:mm", "HH:mm:ss" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Normalizes time string to internal format for consistent storage
        /// </summary>
        /// <param name="timeString">The time string to normalize</param>
        /// <returns>Normalized time string in internal format, or null if parsing fails</returns>
        public static string NormalizeTimeString(string timeString)
        {
            if (ParseToDateTime(timeString, out DateTime parsedTime))
            {
                return FormatTimeStorage(parsedTime);
            }

            return null;
        }

        /// <summary>
        /// Combines a date with a time string to create a full DateTime
        /// </summary>
        /// <param name="date">The date portion</param>
        /// <param name="timeString">The time string</param>
        /// <returns>Combined DateTime, or the original date if time parsing fails</returns>
        public static DateTime CombineDateAndTime(DateTime date, string timeString)
        {
            if (ParseToDateTime(timeString, out DateTime parsedTime))
            {
                return new DateTime(
                    date.Year,
                    date.Month,
                    date.Day,
                    parsedTime.Hour,
                    parsedTime.Minute,
                    parsedTime.Second);
            }

            return date;
        }

        /// <summary>
        /// Ensures a DateTime is in the future by adding days if necessary
        /// </summary>
        /// <param name="dateTime">The DateTime to check</param>
        /// <returns>A future DateTime</returns>
        public static DateTime EnsureFutureDateTime(DateTime dateTime)
        {
            // If the provided datetime is in the past, push it to tomorrow
            if (dateTime <= DateTime.Now)
            {
                return dateTime.AddDays(1);
            }

            return dateTime;
        }

        /// <summary>
        /// Converts a Unix timestamp to a DateTime
        /// </summary>
        /// <param name="unixTimestamp">Unix timestamp (seconds since epoch)</param>
        /// <returns>Corresponding DateTime</returns>
        public static DateTime FromUnixTimestamp(long unixTimestamp)
        {
            // Unix epoch starts at January 1st, 1970
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTimestamp).ToLocalTime();
        }

        /// <summary>
        /// Converts a DateTime to a Unix timestamp
        /// </summary>
        /// <param name="dateTime">DateTime to convert</param>
        /// <returns>Unix timestamp (seconds since epoch)</returns>
        public static long ToUnixTimestamp(DateTime dateTime)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(dateTime.ToUniversalTime() - epoch).TotalSeconds;
        }
    }
}