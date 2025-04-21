using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace claudpro.Utilities
{
    /// <summary>
    /// Provides centralized error handling, logging, and reporting
    /// </summary>
    public static class ErrorHandler
    {
        // Categories for errors
        public enum ErrorCategory
        {
            Database,
            Network,
            Mapping,
            UI,
            Routing,
            Authentication,
            General
        }

        // Severity levels
        public enum ErrorSeverity
        {
            Information,
            Warning,
            Error,
            Critical
        }

        // Path to log file
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RideMatch",
            "logs",
            $"RideMatch_{DateTime.Now:yyyy-MM-dd}.log");

        // Recent errors for status displays
        private static readonly List<(DateTime Time, string Message, ErrorSeverity Severity)> RecentErrors
            = new List<(DateTime, string, ErrorSeverity)>();

        // Maximum number of recent errors to keep
        private const int MaxRecentErrors = 100;

        // Flag to determine if we're in development mode
        private static bool IsDevelopmentMode => Debugger.IsAttached;

        static ErrorHandler()
        {
            // Ensure the log directory exists
            string directory = Path.GetDirectoryName(LogFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Logs an error and optionally displays it to the user
        /// </summary>
        public static void LogError(Exception ex, ErrorCategory category, ErrorSeverity severity,
                                   string userMessage = null, bool displayToUser = true)
        {
            try
            {
                // Build error details
                string errorDetails = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {severity} in {category}: {ex.Message}";
                if (ex.StackTrace != null)
                {
                    errorDetails += Environment.NewLine + "Stack Trace:" + Environment.NewLine + ex.StackTrace;
                }

                // Add inner exception details if present
                if (ex.InnerException != null)
                {
                    errorDetails += Environment.NewLine + "Inner Exception: " + ex.InnerException.Message;
                }

                // Log to file
                LogToFile(errorDetails);

                // Store in recent errors
                StoreRecentError(ex.Message, severity);

                // Display to user if requested
                if (displayToUser)
                {
                    ShowErrorToUser(userMessage ?? ex.Message, severity, ex);
                }

                // Output to debug console in development mode
                if (IsDevelopmentMode)
                {
                    Debug.WriteLine(errorDetails);
                }
            }
            catch
            {
                // Failsafe if error handling itself fails
                try
                {
                    Debug.WriteLine("Error in error handler: Failed to process error: " + ex.Message);
                }
                catch
                {
                    // Last resort - do nothing, we can't even log this
                }
            }
        }

        /// <summary>
        /// Logs a message without an exception
        /// </summary>
        public static void LogMessage(string message, ErrorCategory category, ErrorSeverity severity,
                                     bool displayToUser = false)
        {
            try
            {
                // Build message details
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {severity} in {category}: {message}";

                // Log to file
                LogToFile(logEntry);

                // Store in recent errors if Warning or higher
                if (severity >= ErrorSeverity.Warning)
                {
                    StoreRecentError(message, severity);
                }

                // Display to user if requested
                if (displayToUser)
                {
                    ShowErrorToUser(message, severity, null);
                }

                // Output to debug console in development mode
                if (IsDevelopmentMode)
                {
                    Debug.WriteLine(logEntry);
                }
            }
            catch
            {
                // Failsafe if error handling itself fails
                try
                {
                    Debug.WriteLine("Error in error handler: Failed to log message: " + message);
                }
                catch
                {
                    // Last resort - do nothing, we can't even log this
                }
            }
        }

        /// <summary>
        /// Executes an action with error handling
        /// </summary>
        public static async Task ExecuteWithErrorHandlingAsync(Func<Task> action,
                                                         ErrorCategory category,
                                                         string errorMessage,
                                                         bool displayToUser = true)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                LogError(ex, category, ErrorSeverity.Error, errorMessage, displayToUser);
            }
        }

        /// <summary>
        /// Executes a function with error handling and returns a default value on error
        /// </summary>
        public static async Task<T> ExecuteWithErrorHandlingAsync<T>(Func<Task<T>> function,
                                                               ErrorCategory category,
                                                               string errorMessage,
                                                               T defaultValue = default,
                                                               bool displayToUser = true)
        {
            try
            {
                return await function();
            }
            catch (Exception ex)
            {
                LogError(ex, category, ErrorSeverity.Error, errorMessage, displayToUser);
                return defaultValue;
            }
        }

        /// <summary>
        /// Logs an entry to the log file
        /// </summary>
        private static void LogToFile(string logEntry)
        {
            try
            {
                // Create directory if it doesn't exist
                string directory = Path.GetDirectoryName(LogFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write to log file
                using (StreamWriter writer = File.AppendText(LogFilePath))
                {
                    writer.WriteLine(logEntry);
                }
            }
            catch
            {
                // If we can't log to file, there's not much we can do
                // Just output to debug if possible
                Debug.WriteLine("Failed to write to log file: " + logEntry);
            }
        }

        /// <summary>
        /// Stores an error in the recent errors list
        /// </summary>
        private static void StoreRecentError(string message, ErrorSeverity severity)
        {
            lock (RecentErrors)
            {
                RecentErrors.Add((DateTime.Now, message, severity));

                // Trim list if it gets too long
                if (RecentErrors.Count > MaxRecentErrors)
                {
                    RecentErrors.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Shows an error message to the user
        /// </summary>
        private static void ShowErrorToUser(string message, ErrorSeverity severity, Exception ex)
        {
            try
            {
                // Determine icon based on severity
                MessageBoxIcon icon = MessageBoxIcon.Information;
                string title = "Information";

                switch (severity)
                {
                    case ErrorSeverity.Warning:
                        icon = MessageBoxIcon.Warning;
                        title = "Warning";
                        break;
                    case ErrorSeverity.Error:
                        icon = MessageBoxIcon.Error;
                        title = "Error";
                        break;
                    case ErrorSeverity.Critical:
                        icon = MessageBoxIcon.Stop;
                        title = "Critical Error";
                        break;
                }

                // Ensure UI operations run on the UI thread
                if (Application.OpenForms.Count > 0 && Application.OpenForms[0].InvokeRequired)
                {
                    Application.OpenForms[0].Invoke(new Action(() =>
                    {
                        MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
                    }));
                }
                else
                {
                    MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
                }

                // Log extra details in debug mode
                if (IsDevelopmentMode && ex != null)
                {
                    Debug.WriteLine($"Error details: {ex}");
                }
            }
            catch
            {
                // If showing message fails, try console output
                Console.WriteLine($"ERROR: {message}");
            }
        }

        /// <summary>
        /// Gets recent errors for display in admin interface
        /// </summary>
        public static List<(DateTime Time, string Message, ErrorSeverity Severity)> GetRecentErrors()
        {
            lock (RecentErrors)
            {
                return new List<(DateTime, string, ErrorSeverity)>(RecentErrors);
            }
        }

        /// <summary>
        /// Gets the log file contents for the given date
        /// </summary>
        public static string GetLogFileContents(DateTime date)
        {
            string logFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RideMatch",
                "logs",
                $"RideMatch_{date:yyyy-MM-dd}.log");

            if (File.Exists(logFile))
            {
                return File.ReadAllText(logFile);
            }

            return "No log file found for the specified date.";
        }
    }
}