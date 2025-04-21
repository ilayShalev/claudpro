using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using claudpro.Models;
using claudpro.UI;
using claudpro.Utilities;
using System.Windows.Forms;

namespace claudpro.Services
{
    public class RoutingService
    {
        private MapService mapService;
        private double destinationLat;
        private double destinationLng;
        private string destinationTargetTime;
        public Dictionary<int, RouteDetails> VehicleRouteDetails { get; private set; }

        public RoutingService(MapService mapService, double destinationLat, double destinationLng, string destinationTargetTime)
        {
            this.mapService = mapService ?? throw new ArgumentNullException(nameof(mapService));
            this.destinationLat = destinationLat;
            this.destinationLng = destinationLng;
            this.destinationTargetTime = destinationTargetTime;
            VehicleRouteDetails = new Dictionary<int, RouteDetails>();
        }

        /// <summary>
        /// Displays passengers and vehicles on the map
        /// </summary>
        public void DisplayDataOnMap(GMapControl mapControl, List<Passenger> passengers, List<Vehicle> vehicles)
        {
            try
            {
                if (mapControl == null) return;

                mapControl.Overlays.Clear();

                var passengersOverlay = new GMapOverlay("passengers");
                var vehiclesOverlay = new GMapOverlay("vehicles");
                var destinationOverlay = new GMapOverlay("destination");
                var routesOverlay = new GMapOverlay("routes");

                // Display destination marker
                var destinationMarker = MapOverlays.CreateDestinationMarker(destinationLat, destinationLng);
                destinationOverlay.Markers.Add(destinationMarker);

                // Display passenger markers
                if (passengers != null)
                {
                    foreach (var passenger in passengers)
                    {
                        if (passenger != null)
                        {
                            var marker = MapOverlays.CreatePassengerMarker(passenger);
                            passengersOverlay.Markers.Add(marker);
                        }
                    }
                }

                // Display vehicle markers
                if (vehicles != null)
                {
                    foreach (var vehicle in vehicles)
                    {
                        if (vehicle != null)
                        {
                            var marker = MapOverlays.CreateVehicleMarker(vehicle);
                            vehiclesOverlay.Markers.Add(marker);
                        }
                    }
                }

                mapControl.Overlays.Add(routesOverlay);
                mapControl.Overlays.Add(passengersOverlay);
                mapControl.Overlays.Add(vehiclesOverlay);
                mapControl.Overlays.Add(destinationOverlay);

                mapControl.Zoom = mapControl.Zoom; // Force refresh
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Error,
                    "Failed to display map data", true);
            }
        }

        /// <summary>
        /// Displays solution routes on the map
        /// </summary>
        public void DisplaySolutionOnMap(GMapControl mapControl, Solution solution)
        {
            if (mapControl == null || solution == null) return;

            try
            {
                DisplayDataOnMap(mapControl, new List<Passenger>(), solution.Vehicles);

                var routesOverlay = mapControl.Overlays.FirstOrDefault(o => o.Id == "routes");
                if (routesOverlay == null)
                {
                    routesOverlay = new GMapOverlay("routes");
                    mapControl.Overlays.Add(routesOverlay);
                }
                else
                {
                    routesOverlay.Routes.Clear();
                }

                var colors = MapOverlays.GetRouteColors();

                for (int i = 0; i < solution.Vehicles.Count; i++)
                {
                    var vehicle = solution.Vehicles[i];
                    if (vehicle == null || vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0) continue;

                    var points = new List<PointLatLng>
                    {
                        new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude)
                    };

                    // Add passenger pickup points
                    foreach (var passenger in vehicle.AssignedPassengers)
                    {
                        if (passenger != null)
                        {
                            points.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
                        }
                    }

                    // Add destination point
                    points.Add(new PointLatLng(destinationLat, destinationLng));

                    // Create route with straight lines
                    var routeColor = colors[i % colors.Length];
                    var route = MapOverlays.CreateRoute(points, $"Route {i}", routeColor);
                    routesOverlay.Routes.Add(route);
                }

                mapControl.Zoom = mapControl.Zoom; // Force refresh
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Error,
                    "Failed to display solution on map", true);
            }
        }

        /// <summary>
        /// Gets detailed route information using Google Maps Directions API
        /// </summary>
        public async Task GetGoogleRoutesAsync(GMapControl mapControl, Solution solution, DateTime? targetArrivalTime)
        {
            if (solution == null) return;

            try
            {
                // Initialize overlays only if mapControl is provided
                GMapOverlay routesOverlay = null;
                if (mapControl != null)
                {
                    routesOverlay = mapControl.Overlays.FirstOrDefault(o => o.Id == "routes");
                    if (routesOverlay == null)
                    {
                        routesOverlay = new GMapOverlay("routes");
                        mapControl.Overlays.Add(routesOverlay);
                    }
                    else
                    {
                        routesOverlay.Routes.Clear();
                    }
                }

                VehicleRouteDetails.Clear();
                var colors = mapControl != null ? MapOverlays.GetRouteColors() : null;

                // Ensure targetArrivalTime is in the future
                if (targetArrivalTime.HasValue)
                {
                    targetArrivalTime = TimeFormatUtility.EnsureFutureDateTime(targetArrivalTime.Value);

                    // Log for debugging
                    ErrorHandler.LogMessage(
                        $"Target arrival time for route calculation: {targetArrivalTime.Value:yyyy-MM-dd HH:mm:ss}",
                        ErrorHandler.ErrorCategory.Routing,
                        ErrorHandler.ErrorSeverity.Information);
                }
                else if (!string.IsNullOrEmpty(destinationTargetTime) && TimeFormatUtility.ParseToDateTime(destinationTargetTime, out DateTime parsedTime))
                {
                    // Use destination target time if no specific time provided
                    DateTime baseDate = DateTime.Today;

                    // Use tomorrow if the time has already passed today
                    if (baseDate.Add(parsedTime.TimeOfDay) < DateTime.Now)
                    {
                        baseDate = baseDate.AddDays(1);
                    }

                    targetArrivalTime = baseDate.Add(parsedTime.TimeOfDay);

                    // Log for debugging
                    ErrorHandler.LogMessage(
                        $"Using destination target time: {targetArrivalTime.Value:yyyy-MM-dd HH:mm:ss}",
                        ErrorHandler.ErrorCategory.Routing,
                        ErrorHandler.ErrorSeverity.Information);
                }

                for (int i = 0; i < solution.Vehicles.Count; i++)
                {
                    var vehicle = solution.Vehicles[i];
                    if (vehicle == null || vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0) continue;

                    // Get route details from Google API, passing the target arrival time
                    RouteDetails routeDetails = null;

                    try
                    {
                        routeDetails = await mapService.GetRouteDetailsAsync(vehicle, destinationLat, destinationLng, targetArrivalTime);
                    }
                    catch (Exception ex)
                    {
                        ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Network, ErrorHandler.ErrorSeverity.Warning,
                            "Google API route request failed, using estimated route instead.", false);

                        // Fall back to estimated routes
                        routeDetails = mapService.EstimateRouteDetails(vehicle, destinationLat, destinationLng);
                    }

                    if (routeDetails != null)
                    {
                        VehicleRouteDetails[vehicle.Id] = routeDetails;
                        vehicle.TotalDistance = routeDetails.TotalDistance;
                        vehicle.TotalTime = routeDetails.TotalTime;

                        // Update vehicle departure time from API response
                        if (!string.IsNullOrEmpty(routeDetails.DepartureTime))
                        {
                            // Normalize to standard format
                            string normalizedDepartureTime = TimeFormatUtility.NormalizeTimeString(routeDetails.DepartureTime);
                            if (!string.IsNullOrEmpty(normalizedDepartureTime))
                            {
                                vehicle.DepartureTime = normalizedDepartureTime;

                                ErrorHandler.LogMessage(
                                    $"Updated vehicle {vehicle.Id} departure time to: {vehicle.DepartureTime}",
                                    ErrorHandler.ErrorCategory.Routing,
                                    ErrorHandler.ErrorSeverity.Information);
                            }
                        }
                        else if (targetArrivalTime.HasValue && vehicle.AssignedPassengers.Count > 0)
                        {
                            // If API didn't provide departure time but we have a target arrival time,
                            // calculate departure time based on total travel time
                            DateTime estimatedDeparture = targetArrivalTime.Value.AddMinutes(-routeDetails.TotalTime);
                            vehicle.DepartureTime = TimeFormatUtility.FormatTimeStorage(estimatedDeparture);

                            ErrorHandler.LogMessage(
                                $"Calculated vehicle {vehicle.Id} departure time: {vehicle.DepartureTime} (based on arrival time)",
                                ErrorHandler.ErrorCategory.Routing,
                                ErrorHandler.ErrorSeverity.Information);
                        }

                        // Update pickup times for passengers
                        UpdatePassengerPickupTimes(vehicle, routeDetails, targetArrivalTime);
                    }

                    // Skip route visualization if no map control provided (headless mode)
                    if (mapControl == null) continue;

                    // Create route points
                    var points = new List<PointLatLng>
                    {
                        new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude)
                    };

                    foreach (var passenger in vehicle.AssignedPassengers)
                    {
                        if (passenger != null)
                        {
                            points.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
                        }
                    }

                    points.Add(new PointLatLng(destinationLat, destinationLng));

                    // Get directions from Google Maps
                    List<PointLatLng> routePoints = null;
                    try
                    {
                        routePoints = await mapService.GetGoogleDirectionsAsync(points, targetArrivalTime);
                    }
                    catch (Exception ex)
                    {
                        ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Network, ErrorHandler.ErrorSeverity.Warning,
                            "Failed to get Google direction points, using straight lines instead.", false);
                    }

                    if (routePoints != null && routePoints.Count > 0)
                    {
                        points = routePoints;
                    }

                    // Add route to map
                    var routeColor = colors[i % colors.Length];
                    var route = MapOverlays.CreateRoute(points, $"Route {i}", routeColor);
                    routesOverlay.Routes.Add(route);
                }

                // Refresh map if present
                if (mapControl != null)
                {
                    mapControl.Zoom = mapControl.Zoom; // Force refresh
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Routing, ErrorHandler.ErrorSeverity.Error,
                    "Failed to get Google routes", true);

                // Fallback to calculated routes
                CalculateEstimatedRouteDetails(solution);
            }
        }

        /// <summary>
        /// Updates passenger pickup times based on route details and departure time
        /// </summary>
        private void UpdatePassengerPickupTimes(Vehicle vehicle, RouteDetails routeDetails, DateTime? targetArrivalTime)
        {
            if (vehicle == null || vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0 || routeDetails == null)
                return;

            try
            {
                // First check if the route details already have pickup times from the API
                bool hasApiTimes = false;

                for (int i = 0; i < Math.Min(vehicle.AssignedPassengers.Count, routeDetails.StopDetails.Count); i++)
                {
                    var stopDetail = routeDetails.StopDetails[i];
                    var passenger = vehicle.AssignedPassengers[i];

                    // Check if passenger and stop ids match and there's an arrival time
                    if (stopDetail.PassengerId == passenger.Id && !string.IsNullOrEmpty(stopDetail.EstimatedArrivalTime))
                    {
                        string normalizedPickupTime = TimeFormatUtility.NormalizeTimeString(stopDetail.EstimatedArrivalTime);
                        if (!string.IsNullOrEmpty(normalizedPickupTime))
                        {
                            passenger.EstimatedPickupTime = normalizedPickupTime;
                            hasApiTimes = true;

                            ErrorHandler.LogMessage(
                                $"Set passenger {passenger.Id} pickup time from API: {normalizedPickupTime}",
                                ErrorHandler.ErrorCategory.Routing,
                                ErrorHandler.ErrorSeverity.Information);
                        }
                    }
                }

                // If no API times, calculate based on vehicle departure time or target arrival time
                if (!hasApiTimes)
                {
                    if (!string.IsNullOrEmpty(vehicle.DepartureTime) &&
                        TimeFormatUtility.ParseToDateTime(vehicle.DepartureTime, out DateTime departureTime))
                    {
                        // Calculate forward from departure time
                        CalculatePickupTimesFromDeparture(vehicle, routeDetails, departureTime);
                    }
                    else if (targetArrivalTime.HasValue)
                    {
                        // Calculate backward from arrival time
                        CalculatePickupTimesFromArrival(vehicle, routeDetails, targetArrivalTime.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Routing, ErrorHandler.ErrorSeverity.Warning,
                    "Failed to update passenger pickup times", false);
            }
        }

        /// <summary>
        /// Calculates pickup times based on vehicle departure time
        /// </summary>
        private void CalculatePickupTimesFromDeparture(Vehicle vehicle, RouteDetails routeDetails, DateTime departureTime)
        {
            for (int i = 0; i < Math.Min(vehicle.AssignedPassengers.Count, routeDetails.StopDetails.Count); i++)
            {
                var stopDetail = routeDetails.StopDetails[i];
                var passenger = vehicle.AssignedPassengers[i];

                // Ensure we have the correct passenger
                if (stopDetail.PassengerId == passenger.Id)
                {
                    // Calculate pickup time based on cumulative time from start
                    double cumulativeMinutes = stopDetail.CumulativeTime;
                    DateTime pickupTime = departureTime.AddMinutes(cumulativeMinutes);

                    // Format as HH:mm
                    passenger.EstimatedPickupTime = TimeFormatUtility.FormatTimeStorage(pickupTime);

                    ErrorHandler.LogMessage(
                        $"Calculated passenger {passenger.Id} pickup time from departure: {passenger.EstimatedPickupTime}",
                        ErrorHandler.ErrorCategory.Routing,
                        ErrorHandler.ErrorSeverity.Information);
                }
            }
        }

        /// <summary>
        /// Calculates pickup times based on target arrival time, working backward
        /// </summary>
        private void CalculatePickupTimesFromArrival(Vehicle vehicle, RouteDetails routeDetails, DateTime arrivalTime)
        {
            if (routeDetails.StopDetails.Count == 0) return;

            // Calculate departure time based on total route time
            DateTime departureTime = arrivalTime.AddMinutes(-routeDetails.TotalTime);

            // Store departure time on vehicle
            vehicle.DepartureTime = TimeFormatUtility.FormatTimeStorage(departureTime);

            // Now calculate pickup times forward from departure time
            CalculatePickupTimesFromDeparture(vehicle, routeDetails, departureTime);
        }

        /// <summary>
        /// Calculates estimated route details for a solution without using Google API
        /// </summary>
        public void CalculateEstimatedRouteDetails(Solution solution)
        {
            if (solution == null) return;

            try
            {
                VehicleRouteDetails.Clear();

                foreach (var vehicle in solution.Vehicles)
                {
                    if (vehicle == null || vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
                        continue;

                    var routeDetails = mapService.EstimateRouteDetails(vehicle, destinationLat, destinationLng);
                    if (routeDetails != null)
                    {
                        VehicleRouteDetails[vehicle.Id] = routeDetails;
                        vehicle.TotalDistance = routeDetails.TotalDistance;
                        vehicle.TotalTime = routeDetails.TotalTime;

                        // If we have a target arrival time, calculate departure and pickup times
                        if (!string.IsNullOrEmpty(destinationTargetTime) &&
                            TimeFormatUtility.ParseToDateTime(destinationTargetTime, out DateTime targetTime))
                        {
                            // Use tomorrow for calculations
                            DateTime baseDate = DateTime.Today.AddDays(1);
                            DateTime targetDateTime = baseDate.Add(targetTime.TimeOfDay);

                            CalculatePickupTimesFromArrival(vehicle, routeDetails, targetDateTime);
                        }
                    }
                }

                ErrorHandler.LogMessage(
                    $"Estimated route details for {VehicleRouteDetails.Count} vehicles",
                    ErrorHandler.ErrorCategory.Routing,
                    ErrorHandler.ErrorSeverity.Information);
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Routing, ErrorHandler.ErrorSeverity.Error,
                    "Failed to calculate estimated route details", true);
            }
        }

        /// <summary>
        /// Updates route details with API data
        /// </summary>
        public async Task CalculateRouteDetailsFromApiAsync(Solution solution)
        {
            if (solution == null) return;

            try
            {
                foreach (var vehicle in solution.Vehicles)
                {
                    if (vehicle == null || vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
                        continue;

                    // Parse departure time if available
                    DateTime? departure = null;
                    if (!string.IsNullOrEmpty(vehicle.DepartureTime) &&
                        TimeFormatUtility.ParseToDateTime(vehicle.DepartureTime, out DateTime parsedTime))
                    {
                        departure = DateTime.Today.Add(parsedTime.TimeOfDay);

                        // If it's in the past, use tomorrow
                        if (departure.Value < DateTime.Now)
                        {
                            departure = departure.Value.AddDays(1);
                        }
                    }

                    try
                    {
                        var details = await mapService.GetRouteDetailsAsync(vehicle, destinationLat, destinationLng, departure);
                        if (details != null)
                        {
                            VehicleRouteDetails[vehicle.Id] = details;
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Network, ErrorHandler.ErrorSeverity.Warning,
                            $"Failed to get route details for vehicle {vehicle.Id}, using fallback", false);

                        // Use estimated route as fallback// Use estimated route as fallback
                        var estimatedDetails = mapService.EstimateRouteDetails(vehicle, destinationLat, destinationLng);
                        if (estimatedDetails != null)
                        {
                            VehicleRouteDetails[vehicle.Id] = estimatedDetails;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Routing, ErrorHandler.ErrorSeverity.Error,
                    "Failed to calculate route details from API", true);
            }
        }

        /// <summary>
        /// Validates the solution for constraints like capacity and passenger assignment
        /// </summary>
        public string ValidateSolution(Solution solution, List<Passenger> allPassengers)
        {
            if (solution == null)
                return "No solution to validate.";

            if (allPassengers == null)
                return "No passengers to validate against.";

            try
            {
                var assignedPassengers = new HashSet<int>();
                var capacityExceeded = false;
                var passengersWithMultipleAssignments = new List<int>();

                foreach (var vehicle in solution.Vehicles)
                {
                    if (vehicle == null) continue;

                    if (vehicle.AssignedPassengers != null && vehicle.AssignedPassengers.Count > vehicle.Capacity)
                    {
                        capacityExceeded = true;
                    }

                    if (vehicle.AssignedPassengers != null)
                    {
                        foreach (var passenger in vehicle.AssignedPassengers)
                        {
                            if (passenger == null) continue;

                            if (assignedPassengers.Contains(passenger.Id))
                            {
                                passengersWithMultipleAssignments.Add(passenger.Id);
                            }
                            else
                            {
                                assignedPassengers.Add(passenger.Id);
                            }
                        }
                    }
                }

                // Check that all passengers who need a ride are assigned
                var availablePassengerIds = allPassengers
                    .Where(p => p.IsAvailableTomorrow)
                    .Select(p => p.Id)
                    .ToHashSet();

                bool allAssigned = availablePassengerIds.IsSubsetOf(assignedPassengers);
                int unassignedCount = availablePassengerIds.Count -
                    availablePassengerIds.Intersect(assignedPassengers).Count();

                // Calculate statistics
                double totalDistance = solution.Vehicles
                    .Where(v => v != null)
                    .Sum(v => v.TotalDistance);

                double totalTime = solution.Vehicles
                    .Where(v => v != null)
                    .Sum(v => v.TotalTime);

                double averageTime = solution.Vehicles.Count(v => v != null && v.AssignedPassengers != null && v.AssignedPassengers.Count > 0) > 0
                    ? totalTime / solution.Vehicles.Count(v => v != null && v.AssignedPassengers != null && v.AssignedPassengers.Count > 0)
                    : 0;

                int usedVehicles = solution.Vehicles.Count(v => v != null && v.AssignedPassengers != null && v.AssignedPassengers.Count > 0);

                StringBuilder report = new StringBuilder();
                report.AppendLine("Validation Results:");
                report.AppendLine($"All passengers assigned: {allAssigned}");
                report.AppendLine($"Assigned passengers: {assignedPassengers.Count}/{availablePassengerIds.Count}");

                if (unassignedCount > 0)
                {
                    report.AppendLine($"Unassigned passengers: {unassignedCount}");
                }

                report.AppendLine($"Capacity exceeded: {capacityExceeded}");

                if (passengersWithMultipleAssignments.Count > 0)
                {
                    report.AppendLine($"Passengers with multiple assignments: {passengersWithMultipleAssignments.Count}");
                    report.AppendLine($"IDs: {string.Join(", ", passengersWithMultipleAssignments)}");
                }

                report.AppendLine();
                report.AppendLine("Statistics:");
                report.AppendLine($"Total distance: {totalDistance:F2} km");
                report.AppendLine($"Total time: {totalTime:F2} minutes");
                report.AppendLine($"Average time per vehicle: {averageTime:F2} minutes");
                report.AppendLine($"Used vehicles: {usedVehicles}/{solution.Vehicles.Count(v => v != null)}");

                return report.ToString();
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Routing, ErrorHandler.ErrorSeverity.Error,
                    "Failed to validate solution", false);

                return $"Error validating solution: {ex.Message}";
            }
        }
    }
}