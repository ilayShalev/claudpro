using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;
using claudpro.Models;
using claudpro.Utilities;

namespace claudpro.Services
{
    public partial class DatabaseService
    {
        // Current database schema version
        private const int CurrentSchemaVersion = 2;
        private string dbFilePath;


        /// <summary>
        /// Initializes the database if it doesn't exist or updates it if needed
        /// </summary>
        private void InitializeDatabase()
        {
            try
            {
                string fileName = connectionString.Replace("Data Source=", "").Split(';')[0];
                // Check if database exists
                bool createNew = !File.Exists(dbFilePath);

                if (createNew)
                {
                    CreateDatabase();
                    SetDatabaseVersion(CurrentSchemaVersion);
                    LogDatabaseAction("Database created with schema version " + CurrentSchemaVersion);
                }
                else
                {
                    // Get current schema version from database
                    int version = GetDatabaseVersion();
                    if (version < CurrentSchemaVersion)
                    {
                        UpdateDatabaseSchema(version);
                        SetDatabaseVersion(CurrentSchemaVersion);
                        LogDatabaseAction($"Database upgraded from version {version} to {CurrentSchemaVersion}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogDatabaseAction($"Error initializing database: {ex.Message}", isError: true);
                throw new Exception($"Failed to initialize database: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the current database schema version
        /// </summary>
        private int GetDatabaseVersion()
        {
            // Check if version table exists
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='DatabaseVersion'";
                var result = cmd.ExecuteScalar();

                if (result == null)
                {
                    // Table doesn't exist, create it and set version to 1
                    CreateVersionTable();
                    return 1;
                }
            }

            // Get version
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "SELECT Version FROM DatabaseVersion ORDER BY ID DESC LIMIT 1";
                var result = cmd.ExecuteScalar();

                if (result == null)
                {
                    // No version record, assume version 1
                    using (var insertCmd = new SQLiteCommand(connection))
                    {
                        insertCmd.CommandText = "INSERT INTO DatabaseVersion (Version, UpdatedDate) VALUES (1, @Date)";
                        insertCmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        insertCmd.ExecuteNonQuery();
                    }
                    return 1;
                }

                return Convert.ToInt32(result);
            }
        }

        /// <summary>
        /// Creates the database version tracking table
        /// </summary>
        private void CreateVersionTable()
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS DatabaseVersion (
                        ID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Version INTEGER NOT NULL,
                        UpdatedDate TEXT NOT NULL
                    )";
                cmd.ExecuteNonQuery();

                // Insert initial version
                cmd.CommandText = "INSERT INTO DatabaseVersion (Version, UpdatedDate) VALUES (1, @Date)";
                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Sets the database schema version
        /// </summary>
        private void SetDatabaseVersion(int version)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = "INSERT INTO DatabaseVersion (Version, UpdatedDate) VALUES (@Version, @Date)";
                cmd.Parameters.AddWithValue("@Version", version);
                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Updates the database schema based on the current version
        /// </summary>
        private void UpdateDatabaseSchema(int currentVersion)
        {
            // Use a transaction for all schema updates
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Apply migrations in order
                    if (currentVersion < 2)
                    {
                        ApplyVersion2Migrations();
                    }

                    // Add future schema updates here
                    // if (currentVersion < 3) ApplyVersion3Migrations();

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    LogDatabaseAction($"Schema migration failed: {ex.Message}", isError: true);
                    throw;
                }
            }
        }

        /// <summary>
        /// Applies version 2 schema migrations
        /// </summary>
        private void ApplyVersion2Migrations()
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                // Check for and add DepartureTime column to RouteDetails
                if (!ColumnExists("RouteDetails", "DepartureTime"))
                {
                    cmd.CommandText = "ALTER TABLE RouteDetails ADD COLUMN DepartureTime TEXT";
                    cmd.ExecuteNonQuery();
                    LogDatabaseAction("Added DepartureTime column to RouteDetails");
                }

                // Check for and add DepartureTime column to Vehicles
                if (!ColumnExists("Vehicles", "DepartureTime"))
                {
                    cmd.CommandText = "ALTER TABLE Vehicles ADD COLUMN DepartureTime TEXT";
                    cmd.ExecuteNonQuery();
                    LogDatabaseAction("Added DepartureTime column to Vehicles");
                }

                // Check for and add EstimatedPickupTime column to Passengers
                if (!ColumnExists("Passengers", "EstimatedPickupTime"))
                {
                    cmd.CommandText = "ALTER TABLE Passengers ADD COLUMN EstimatedPickupTime TEXT";
                    cmd.ExecuteNonQuery();
                    LogDatabaseAction("Added EstimatedPickupTime column to Passengers");
                }

                // Check for and add EstimatedPickupTime column to PassengerAssignments
                if (!ColumnExists("PassengerAssignments", "EstimatedPickupTime"))
                {
                    cmd.CommandText = "ALTER TABLE PassengerAssignments ADD COLUMN EstimatedPickupTime TEXT";
                    cmd.ExecuteNonQuery();
                    LogDatabaseAction("Added EstimatedPickupTime column to PassengerAssignments");
                }

                // Create database logs table if it doesn't exist
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS DatabaseLogs (
                        LogID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Timestamp TEXT NOT NULL,
                        Action TEXT NOT NULL,
                        IsError INTEGER NOT NULL DEFAULT 0
                    )";
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Checks if a column exists in a table
        /// </summary>
        private bool ColumnExists(string tableName, string columnName)
        {
            using (var cmd = new SQLiteCommand(connection))
            {
                cmd.CommandText = $"PRAGMA table_info({tableName})";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader["name"].ToString() == columnName)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Logs database actions for audit and debugging
        /// </summary>
        private void LogDatabaseAction(string action, bool isError = false)
        {
            try
            {
                // Log to console in debug mode
                Console.WriteLine($"Database: {action}");

                // Check if log table exists
                using (var cmd = new SQLiteCommand(connection))
                {
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='DatabaseLogs'";
                    var result = cmd.ExecuteScalar();

                    if (result == null)
                    {
                        // Table doesn't exist, try to create it
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS DatabaseLogs (
                                LogID INTEGER PRIMARY KEY AUTOINCREMENT,
                                Timestamp TEXT NOT NULL,
                                Action TEXT NOT NULL,
                                IsError INTEGER NOT NULL DEFAULT 0
                            )";
                        cmd.ExecuteNonQuery();
                    }

                    // Log the action
                    cmd.CommandText = "INSERT INTO DatabaseLogs (Timestamp, Action, IsError) VALUES (@Timestamp, @Action, @IsError)";
                    cmd.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@Action", action);
                    cmd.Parameters.AddWithValue("@IsError", isError ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Suppress errors in logging to prevent cascading failures
            }
        }

        /// <summary>
        /// Gets the database logs for admin viewing
        /// </summary>
        public async Task<List<(DateTime Timestamp, string Action, bool IsError)>> GetDatabaseLogsAsync(int limit = 100)
        {
            var logs = new List<(DateTime Timestamp, string Action, bool IsError)>();

            try
            {
                using (var cmd = new SQLiteCommand(connection))
                {
                    cmd.CommandText = @"
                        SELECT Timestamp, Action, IsError 
                        FROM DatabaseLogs 
                        ORDER BY LogID DESC 
                        LIMIT @Limit";
                    cmd.Parameters.AddWithValue("@Limit", limit);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            logs.Add((
                                DateTime.Parse(reader.GetString(0)),
                                reader.GetString(1),
                                reader.GetInt32(2) == 1
                            ));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving database logs: {ex.Message}");
            }

            return logs;
        }
    }
}