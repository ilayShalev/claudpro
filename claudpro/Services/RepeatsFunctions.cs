using System;

namespace claudpro.UI
{
    public static class TimeFormatUtility
    {
        /// <summary>
        /// Formats a time string consistently in 24-hour format
        /// </summary>
        /// <param name="timeString">The time string to format</param>
        /// <returns>Formatted time string in 24-hour format (HH:mm)</returns>
        public static string FormatTimeDisplay(string timeString)
        {
            if (string.IsNullOrEmpty(timeString))
                return "Not scheduled";

            // Try to parse the time
            if (DateTime.TryParse(timeString, out DateTime parsedTime))
            {
                // Return in 24-hour format for consistency
                return parsedTime.ToString("HH:mm");
            }

            // Return the original if parsing fails
            return timeString;
        }
    }
}