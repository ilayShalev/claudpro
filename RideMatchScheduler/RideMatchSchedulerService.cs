using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Timers;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;
using claudpro.Models;
using claudpro.Services;

namespace RideMatchScheduler
{
    public class RideMatchSchedulerService : ServiceBase
    {
        private Timer schedulerTimer;
        private DatabaseService dbService;
        private MapService mapService;
        private RoutingService routingService;

        private string logFilePath;
        private bool isRunningTask = false;

        public RideMatchSchedulerService()
        {
            InitializeComponent();

            // Initialize services
            string dbPath = ConfigurationManager.AppSettings["DatabasePath"] ?? "ridematch.db";

            // Make sure it's an absolute path
            if (!Path.IsPathRooted(dbPath))
            {
                // Use service location as base path
                dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath);
            }

            string apiKey = ConfigurationManager.AppSettings["GoogleApiKey"] ?? "";

            // Set up logging path
            logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RideMatchScheduler.log");

            // Create database directory if it doesn't exist
            string dbDirectory = Path.GetDirectoryName(dbPath);
            if (!Directory.Exists(dbDirectory) && !string.IsNullOrEmpty(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
            }

            try
            {
                dbService = new DatabaseService(dbPath);
                mapService = new MapService(apiKey);

                // Initialize routing service with default destination
                try
                {
                    // Initialize routing service with default destination
                    var defaultDest = dbService.GetDestinationAsync().GetAwaiter().GetResult();
                    routingService = new RoutingService(mapService, defaultDest.Latitude, defaultDest.Longitude);
                    Log($"Routing service initialized with destination: {defaultDest.Name}");
                }
                catch (Exception ex)
                {
                    Log($"Failed to initialize routing service: {ex.Message}");
                    // Initialize with default values just to avoid null references
                    routingService = new RoutingService(mapService, 0, 0);
                }
            }
            catch (Exception ex)
            {
                Log($"Error initializing services: {ex.Message}");
                throw; // Re-throw to prevent service from starting with initialization errors
            }

            // Set service name
            ServiceName = "RideMatchSchedulerService";
        }
        private void InitializeComponent()
        {
            // Service component initialization
            this.CanStop = true;
            this.CanPauseAndContinue = true;  // Support pause/continue
            this.CanShutdown = true;          // Proper shutdown handling
            this.AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            Log("RideMatch Scheduler Service started");

            try
            {
                // Set timer to check every minute if it's time to run the algorithm
                schedulerTimer = new Timer(60000); // 60,000 ms = 1 minute
                schedulerTimer.Elapsed += CheckScheduleTime;
                schedulerTimer.Start();

                // Load initial settings
                Task.Run(async () => {
                    try
                    {
                        var settings = await dbService.GetSchedulingSettingsAsync();
                        Log($"Service started. Scheduling is {(settings.IsEnabled ? "enabled" : "disabled")}. " +
                            $"Scheduled time: {settings.ScheduledTime.ToString("HH:mm:ss")}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Error loading settings: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"Error in OnStart: {ex.Message}");
                throw; // Re-throw to notify SCM of startup failure
            }
        }

        protected override void OnStop()
        {
            Log("RideMatch Scheduler Service stopping");

            try
            {
                schedulerTimer?.Stop();
                schedulerTimer?.Dispose();

                // Wait for any running task to complete
                int waitCount = 0;
                while (isRunningTask && waitCount < 30) // Wait up to 30 seconds
                {
                    System.Threading.Thread.Sleep(1000);
                    waitCount++;
                    Log($"Waiting for running task to complete... ({waitCount}/30)");
                }

                dbService?.Dispose();
                Log("RideMatch Scheduler Service stopped");
            }
            catch (Exception ex)
            {
                Log($"Error in OnStop: {ex.Message}");
            }
        }

        protected override void OnPause()
        {
            try
            {
                schedulerTimer?.Stop();
                Log("RideMatch Scheduler Service paused");
            }
            catch (Exception ex)
            {
                Log($"Error in OnPause: {ex.Message}");
            }
        }

        protected override void OnContinue()
        {
            try
            {
                schedulerTimer?.Start();
                Log("RideMatch Scheduler Service resumed");
            }
            catch (Exception ex)
            {
                Log($"Error in OnContinue: {ex.Message}");
            }
        }

        protected override void OnShutdown()
        {
            Log("System shutdown detected");

            try
            {
                // Same cleanup as OnStop
                schedulerTimer?.Stop();
                schedulerTimer?.Dispose();
                dbService?.Dispose();
                Log("RideMatch Scheduler Service shutdown complete");
            }
            catch (Exception ex)
            {
                Log($"Error in OnShutdown: {ex.Message}");
            }
        }

        private async void CheckScheduleTime(object sender, ElapsedEventArgs e)
        {
            if (isRunningTask) return; // Prevent multiple runs at once

            try
            {
                isRunningTask = true;

                // Get current time
                DateTime now = DateTime.Now;

                // Get the scheduled time from database
                var settings = await dbService.GetSchedulingSettingsAsync();

                // Check if scheduling is enabled and if it's the correct time
                if (settings.IsEnabled &&
                    now.Hour == settings.ScheduledTime.Hour &&
                    now.Minute == settings.ScheduledTime.Minute)
                {
                    Log($"Running scheduled route calculation at {now}");
                    await RunAlgorithmAsync();
                }
            }
            catch (Exception ex)
            {
                Log($"Error in scheduler: {ex.Message}");
            }
            finally
            {
                isRunningTask = false;
            }
        }

        // Update the RunAlgorithmAsync method in RideMatchSchedulerService.cs
        // to use Google API for time calculations based on desired arrival time


        // עדכון מתודת RunAlgorithmAsync ב-RideMatchSchedulerService.cs
        private async Task RunAlgorithmAsync()
        {
            try
            {
                // Get destination information
                var destination = await dbService.GetDestinationAsync();
                Log($"Using destination: {destination.Name}, Location: {destination.Latitude}, {destination.Longitude}");
                Log($"Target arrival time: {destination.TargetTime}");

                // Get available vehicles and passengers
                var vehicles = await dbService.GetAvailableVehiclesAsync();
                var passengers = await dbService.GetAvailablePassengersAsync();

                // Only run if there are passengers and vehicles
                if (passengers.Count > 0 && vehicles.Count > 0)
                {
                    Log($"Running algorithm with {passengers.Count} passengers and {vehicles.Count} vehicles");

                    // Create a routing service
                    var routingService = new RoutingService(mapService, destination.Latitude, destination.Longitude);

                    // Create the solver
                    var solver = new RideSharingGenetic(
                        passengers,
                        vehicles,
                        200, // Population size
                        destination.Latitude,
                        destination.Longitude,
                        GetTargetTimeInMinutes(destination.TargetTime)
                    );

                    // Run the algorithm
                    var solution = solver.Solve(150); // Generations

                    if (solution != null)
                    {
                        // After running the algorithm, apply routes based on settings
                        try
                        {
                            // Check if Google Routes API should be used
                            string useGoogleApi = await dbService.GetSettingAsync("UseGoogleRoutesAPI", "1");
                            bool shouldUseGoogleApi = useGoogleApi == "1";

                            // Always calculate estimated routes first as a fallback
                            routingService.CalculateEstimatedRouteDetails(solution);

                            if (shouldUseGoogleApi)
                            {
                                try
                                {
                                    // Parse target arrival time
                                    if (!TimeSpan.TryParse(destination.TargetTime, out TimeSpan targetTime))
                                    {
                                        targetTime = new TimeSpan(8, 0, 0); // Default to 8:00 AM
                                    }

                                    // Calculate target date time for tomorrow
                                    DateTime targetDateTime = DateTime.Today.AddDays(1).Add(targetTime);

                                    // Try to get routes from Google API with target arrival time
                                    Log("Fetching routes from Google Maps API with arrival_time parameter...");
                                    await routingService.GetGoogleRoutesAsync(null, solution, targetDateTime);
                                    Log("Successfully retrieved routes from Google Maps API");
                                }
                                catch (Exception ex)
                                {
                                    // If Google API fails, we already have the estimated routes calculated
                                    Log($"Google API request failed: {ex.Message}. Using estimated routes instead.");
                                }
                            }
                            else
                            {
                                Log("Using estimated routes (Google API disabled in settings)");
                            }

                            // Calculate backward from target arrival time to determine pickup times
                            await CalculatePickupTimesBasedOnTargetArrival(solution, destination.TargetTime, routingService);
                        }
                        catch (Exception ex)
                        {
                            Log($"Error calculating routes: {ex.Message}");
                        }

                        // Save the solution to database for tomorrow's date
                        string tomorrowDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
                        int routeId = await dbService.SaveSolutionAsync(solution, tomorrowDate);

                        // Count assigned passengers and used vehicles
                        int assignedPassengers = solution.Vehicles.Sum(v => v.AssignedPassengers?.Count ?? 0);
                        int usedVehicles = solution.Vehicles.Count(v => v.AssignedPassengers?.Count > 0);

                        Log($"Algorithm completed and saved as route #{routeId}");
                        Log($"Assigned {assignedPassengers} passengers to {usedVehicles} vehicles");

                        // Log the scheduling run explicitly to the database
                        await dbService.LogSchedulingRunAsync(
                            DateTime.Now,
                            "Success",
                            usedVehicles,
                            assignedPassengers,
                            $"Created routes for {tomorrowDate}"
                        );
                    }
                    else
                    {
                        Log("Algorithm failed to find a valid solution");
                        await dbService.LogSchedulingRunAsync(
                            DateTime.Now,
                            "Failed",
                            0,
                            0,
                            "Algorithm failed to find a valid solution"
                        );
                    }
                }
                else
                {
                    Log("No passengers or vehicles available for tomorrow - skipping algorithm run");
                    await dbService.LogSchedulingRunAsync(
                        DateTime.Now,
                        "Skipped",
                        0,
                        0,
                        $"Insufficient participants: {passengers.Count} passengers, {vehicles.Count} vehicles"
                    );
                }
            }
            catch (Exception ex)
            {
                Log($"Error running algorithm: {ex.Message}");
                Log($"Stack trace: {ex.StackTrace}");

                try
                {
                    await dbService.LogSchedulingRunAsync(
                        DateTime.Now,
                        "Error",
                        0,
                        0,
                        ex.Message
                    );
                }
                catch
                {
                    // Just in case writing to the database also fails
                }

                throw; // Re-throw to show error to the user
            }
        }
        // New method to calculate pickup times based on desired arrival time at destination
        private async Task CalculatePickupTimesBasedOnTargetArrival(Solution solution, string targetTimeString, RoutingService routingService)
        {
            // Parse target arrival time
            if (!TimeSpan.TryParse(targetTimeString, out TimeSpan targetTime))
            {
                targetTime = new TimeSpan(8, 0, 0); // Default to 8:00 AM
            }

            // Get the target time as DateTime for tomorrow (users are scheduling rides for tomorrow)
            DateTime targetDateTime = DateTime.Today.AddDays(1).Add(targetTime);

            // וידוא שזמן היעד הוא באמת עתידי
            if (targetDateTime <= DateTime.Now)
            {
                // אם הזמן לא בעתיד, נוסיף עוד יום
                targetDateTime = targetDateTime.AddDays(1);
            }

            Log($"Calculating pickup times based on target arrival time: {targetDateTime.ToString("yyyy-MM-dd HH:mm:ss")}");

            try
            {
                // Call the Google Routes API with arrival_time parameter
                await routingService.GetGoogleRoutesAsync(null, solution, targetDateTime);

                // הנתונים כבר עודכנו בכלי הרכב והנוסעים בתוך GetGoogleRoutesAsync
                // נעדכן את התצוגה במקרה שיש מידע חסר

                foreach (var vehicle in solution.Vehicles)
                {
                    if (vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
                        continue;

                    RouteDetails routeDetails = null;
                    if (routingService.VehicleRouteDetails.ContainsKey(vehicle.Id))
                    {
                        routeDetails = routingService.VehicleRouteDetails[vehicle.Id];
                        Log($"Found Google route details for vehicle {vehicle.Id}: {routeDetails.TotalTime} minutes total trip time");

                        // השתמש בזמן היציאה שהתקבל מגוגל או חשב לפי זמן הגעה
                        if (!string.IsNullOrEmpty(routeDetails.DepartureTime))
                        {
                            vehicle.DepartureTime = routeDetails.DepartureTime;
                            Log($"Vehicle {vehicle.Id} departure time from Google API: {vehicle.DepartureTime}");
                        }
                        else if (!string.IsNullOrEmpty(vehicle.DepartureTime))
                        {
                            Log($"Vehicle {vehicle.Id} already has departure time: {vehicle.DepartureTime}");
                        }
                        else
                        {
                            // חישוב ידני של זמן היציאה אם לא התקבל מה-API
                            double totalTripTime = routeDetails.TotalTime;
                            DateTime driverStartTime = targetDateTime.AddMinutes(-totalTripTime);
                            vehicle.DepartureTime = driverStartTime.ToString("HH:mm");
                            Log($"Vehicle {vehicle.Id} calculated departure time: {vehicle.DepartureTime}");
                        }

                        // עדכון זמני איסוף לנוסעים
                        for (int i = 0; i < vehicle.AssignedPassengers.Count; i++)
                        {
                            var passenger = vehicle.AssignedPassengers[i];

                            // מצא את הפרטים המתאימים לנוסע בתוצאות המסלול
                            var stopDetail = routeDetails.StopDetails.FirstOrDefault(s => s.PassengerId == passenger.Id);
                            if (stopDetail != null)
                            {
                                // אם יש זמן הגעה מדויק מהAPI, השתמש בו
                                if (!string.IsNullOrEmpty(stopDetail.EstimatedArrivalTime))
                                {
                                    passenger.EstimatedPickupTime = stopDetail.EstimatedArrivalTime;
                                    Log($"Passenger {passenger.Id} ({passenger.Name}) pickup time from Google API: {passenger.EstimatedPickupTime}");
                                }
                                // אחרת חשב זמן הגעה לפי זמן נסיעה מצטבר
                                else if (string.IsNullOrEmpty(passenger.EstimatedPickupTime))
                                {
                                    double cumulativeTimeFromStart = stopDetail.CumulativeTime;

                                    // אם אין זמן יציאה לרכב, חשב לפי זמן הגעה רצוי
                                    DateTime pickupTime;
                                    if (!string.IsNullOrEmpty(vehicle.DepartureTime) && DateTime.TryParse(vehicle.DepartureTime, out DateTime departureTime))
                                    {
                                        // חבר את השעה לתאריך של מחר
                                        DateTime fullDepartureTime = DateTime.Today.AddDays(1).Date + departureTime.TimeOfDay;

                                        // וידוא שזמן היציאה הוא בעתיד
                                        if (fullDepartureTime <= DateTime.Now)
                                        {
                                            fullDepartureTime = fullDepartureTime.AddDays(1);
                                        }

                                        pickupTime = fullDepartureTime.AddMinutes(cumulativeTimeFromStart);
                                    }
                                    else
                                    {
                                        // חשב לאחור מזמן הגעה רצוי
                                        pickupTime = targetDateTime.AddMinutes(-(routeDetails.TotalTime - cumulativeTimeFromStart));
                                    }

                                    passenger.EstimatedPickupTime = pickupTime.ToString("HH:mm");
                                    Log($"Passenger {passenger.Id} ({passenger.Name}) calculated pickup time: {passenger.EstimatedPickupTime}");
                                }
                            }
                            else
                            {
                                Log($"Warning: No stop details found for passenger {passenger.Id} in vehicle {vehicle.Id}");
                            }
                        }
                    }
                    else
                    {
                        Log($"Warning: No route details found for vehicle {vehicle.Id}. Using estimates.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error when getting route details with arrival time: {ex.Message}");
                Log($"Falling back to estimated calculations");

                // המשך עם הלוגיקה המקורית לחישוב זמנים במקרה של כישלון
                foreach (var vehicle in solution.Vehicles)
                {
                    if (vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
                        continue;

                    RouteDetails routeDetails = null;
                    if (routingService.VehicleRouteDetails.ContainsKey(vehicle.Id))
                    {
                        routeDetails = routingService.VehicleRouteDetails[vehicle.Id];
                        Log($"Using estimated route details for vehicle {vehicle.Id}: {routeDetails.TotalTime} minutes total trip time");
                    }
                    else
                    {
                        Log($"Warning: No route details found for vehicle {vehicle.Id}. Cannot calculate times.");
                        continue;
                    }

                    // Get total trip time from start to destination in minutes
                    double totalTripTime = routeDetails.TotalTime;

                    // Calculate when driver needs to start to arrive at destination at target time
                    DateTime driverStartTime = targetDateTime.AddMinutes(-totalTripTime);

                    // Store the driver's departure time using 24-hour format
                    vehicle.DepartureTime = driverStartTime.ToString("HH:mm");
                    Log($"Vehicle {vehicle.Id} estimated departure time: {vehicle.DepartureTime}");

                    // Now calculate each passenger's pickup time based on cumulative time from start
                    for (int i = 0; i < vehicle.AssignedPassengers.Count; i++)
                    {
                        var passenger = vehicle.AssignedPassengers[i];

                        // Find corresponding stop detail
                        var stopDetail = routeDetails.StopDetails.FirstOrDefault(s => s.PassengerId == passenger.Id);
                        if (stopDetail != null)
                        {
                            double cumulativeTimeFromStart = stopDetail.CumulativeTime;

                            // Calculate pickup time based on driver start time plus cumulative time to this passenger
                            DateTime pickupTime = driverStartTime.AddMinutes(cumulativeTimeFromStart);

                            // Use 24-hour format for consistent time representation
                            passenger.EstimatedPickupTime = pickupTime.ToString("HH:mm");

                            Log($"Passenger {passenger.Id} ({passenger.Name}) estimated pickup time: {passenger.EstimatedPickupTime}");
                        }
                        else
                        {
                            Log($"Warning: No stop details found for passenger {passenger.Id} in vehicle {vehicle.Id}");
                        }
                    }
                }
            }
        }
        private int GetTargetTimeInMinutes(string targetTime)
        {
            // Convert a time string like "08:00:00" to minutes from midnight
            if (TimeSpan.TryParse(targetTime, out TimeSpan time))
            {
                return (int)time.TotalMinutes;
            }

            // Default to 8:00 AM (480 minutes)
            return 480;
        }

        private void Log(string message)
        {
            try
            {
                // Create the directory if it doesn't exist
                string directory = Path.GetDirectoryName(logFilePath);
                if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Use a lock to prevent multiple threads from writing simultaneously
                lock (this)
                {
                    using (StreamWriter writer = File.AppendText(logFilePath))
                    {
                        writer.WriteLine($"{DateTime.Now}: {message}");
                    }
                }
            }
            catch
            {
                // Logging should never crash the service
                // We can't really log a logging failure...
            }
        }
    }
}