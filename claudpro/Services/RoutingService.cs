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
        private  MapService mapService;
        private  double destinationLat;
        private  double destinationLng;
        private string destinationTargetTime;
        public Dictionary<int, RouteDetails> VehicleRouteDetails { get; private set; }

        public RoutingService(MapService mapService, double destinationLat, double destinationLng, string destinationTargetTime)
        {
            this.mapService = mapService;
            this.destinationLat = destinationLat;
            this.destinationLng = destinationLng;


            DateTime? targetDateTime = null;

            if (TimeSpan.TryParse(destinationTargetTime, out TimeSpan targetTime))
            {
                targetDateTime = DateTime.Today.AddDays(1).Add(targetTime); // למשל: מחר בשעה 08:00
            }

            this.destinationTargetTime = destinationTargetTime;
            VehicleRouteDetails = new Dictionary<int, RouteDetails>();

        }

        /// <summary>
        /// Displays passengers and vehicles on the map
        /// </summary>
        public void DisplayDataOnMap(GMapControl mapControl, List<Passenger> passengers, List<Vehicle> vehicles)
        {
            mapControl.Overlays.Clear();

            var passengersOverlay = new GMapOverlay("passengers");
            var vehiclesOverlay = new GMapOverlay("vehicles");
            var destinationOverlay = new GMapOverlay("destination");
            var routesOverlay = new GMapOverlay("routes");

            // Display destination marker
            var destinationMarker = MapOverlays.CreateDestinationMarker(destinationLat, destinationLng);
            destinationOverlay.Markers.Add(destinationMarker);

            // Display passenger markers
            foreach (var passenger in passengers)
            {
                var marker = MapOverlays.CreatePassengerMarker(passenger);
                passengersOverlay.Markers.Add(marker);
            }

            // Display vehicle markers
            foreach (var vehicle in vehicles)
            {
                var marker = MapOverlays.CreateVehicleMarker(vehicle);
                vehiclesOverlay.Markers.Add(marker);
            }

            mapControl.Overlays.Add(routesOverlay);
            mapControl.Overlays.Add(passengersOverlay);
            mapControl.Overlays.Add(vehiclesOverlay);
            mapControl.Overlays.Add(destinationOverlay);

            mapControl.Zoom = mapControl.Zoom; // Force refresh
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
                MessageBox.Show($"Error displaying solution on map: {ex.Message}",
                    "Map Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        /// <summary>
        /// Gets detailed route information using Google Maps Directions API
        /// </summary>
        public async Task GetGoogleRoutesAsync(GMapControl mapControl, Solution solution, DateTime? destinationTargetTime)
        {
            if (solution == null) return;

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

            // Log the target arrival time for debugging
            if (destinationTargetTime.HasValue)
            {
                Console.WriteLine($"Target arrival time for route calculation: {destinationTargetTime.Value.ToString("yyyy-MM-dd HH:mm:ss")}");
            }

            for (int i = 0; i < solution.Vehicles.Count; i++)
            {
                var vehicle = solution.Vehicles[i];
                if (vehicle.AssignedPassengers.Count == 0) continue;

                // Get route details from Google API, passing the target arrival time
                var routeDetails = await mapService.GetRouteDetailsAsync(vehicle, destinationLat, destinationLng, destinationTargetTime);
                if (routeDetails != null)
                {
                    VehicleRouteDetails[vehicle.Id] = routeDetails;
                    vehicle.TotalDistance = routeDetails.TotalDistance;
                    vehicle.TotalTime = routeDetails.TotalTime;

                    // Update vehicle departure time from API response
                    if (!string.IsNullOrEmpty(routeDetails.DepartureTime))
                    {
                        vehicle.DepartureTime = routeDetails.DepartureTime;
                        Console.WriteLine($"Updated vehicle {vehicle.Id} departure time to: {vehicle.DepartureTime} (24-hour format)");
                    }
                    else if (destinationTargetTime.HasValue && vehicle.AssignedPassengers.Count > 0)
                    {
                        // If API didn't provide departure time but we have a target arrival time,
                        // calculate departure time based on total travel time
                        DateTime estimatedDeparture = destinationTargetTime.Value.AddMinutes(-routeDetails.TotalTime);
                        vehicle.DepartureTime = estimatedDeparture.ToString("HH:mm");
                        Console.WriteLine($"Calculated vehicle {vehicle.Id} departure time: {vehicle.DepartureTime} (based on arrival time)");
                    }

                    // Update pickup times for passengers
                    for (int j = 0; j < vehicle.AssignedPassengers.Count; j++)
                    {
                        if (j < routeDetails.StopDetails.Count)
                        {
                            var stopDetail = routeDetails.StopDetails[j];
                            var passenger = vehicle.AssignedPassengers[j];

                            // Ensure the matching is correct (StopDetails order might not match AssignedPassengers order)
                            if (stopDetail.PassengerId == passenger.Id && !string.IsNullOrEmpty(stopDetail.EstimatedArrivalTime))
                            {
                                passenger.EstimatedPickupTime = stopDetail.EstimatedArrivalTime;
                                Console.WriteLine($"Updated passenger {passenger.Id} pickup time to: {passenger.EstimatedPickupTime}");
                            }
                            else if (string.IsNullOrEmpty(passenger.EstimatedPickupTime) && !string.IsNullOrEmpty(vehicle.DepartureTime))
                            {
                                // If no arrival time from API, estimate based on cumulative time
                                double cumulativeMinutes = stopDetail.CumulativeTime;

                                if (DateTime.TryParse(vehicle.DepartureTime, out DateTime depTime))
                                {
                                    DateTime estPickupTime = depTime.AddMinutes(cumulativeMinutes);
                                    passenger.EstimatedPickupTime = estPickupTime.ToString("HH:mm");
                                    Console.WriteLine($"Calculated passenger {passenger.Id} pickup time: {passenger.EstimatedPickupTime} (based on departure + cumulative time)");
                                }
                            }
                        }
                    }
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
                    points.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
                }

                points.Add(new PointLatLng(destinationLat, destinationLng));

                // Get directions from Google Maps - pass the target arrival time to get traffic-based directions
                var routePoints = await mapService.GetGoogleDirectionsAsync(points, destinationTargetTime);

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

        /// Calculates estimated route details for a solution without using Google API
        /// </summary>
        public void CalculateEstimatedRouteDetails(Solution solution)
        {
            if (solution == null) return;

            VehicleRouteDetails.Clear();

            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers.Count == 0) continue;

                var routeDetails = mapService.EstimateRouteDetails(vehicle, destinationLat, destinationLng);
                if (routeDetails != null)
                {
                    VehicleRouteDetails[vehicle.Id] = routeDetails;
                    vehicle.TotalDistance = routeDetails.TotalDistance;
                    vehicle.TotalTime = routeDetails.TotalTime;
                }
            }
        }


        public async Task CalculateRouteDetailsFromApiAsync(Solution solution)
        {
            foreach (var vehicle in solution.Vehicles)
            {
                DateTime? departure = DateTime.TryParse(vehicle.DepartureTime, out var dt) ? dt : (DateTime?)null;

                var details = await mapService.GetRouteDetailsAsync(vehicle, destinationLat, destinationLng, departure);
                VehicleRouteDetails[vehicle.Id] = details;
            }
        }


        /// <summary>
        /// Validates the solution for constraints like capacity and passenger assignment
        /// </summary>
        public string ValidateSolution(Solution solution, List<Passenger> allPassengers)
        {
            if (solution == null) return "No solution to validate.";

            var assignedPassengers = new HashSet<int>();
            var capacityExceeded = false;
            var passengersWithMultipleAssignments = new List<int>();

            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers.Count > vehicle.Capacity)
                {
                    capacityExceeded = true;
                }

                foreach (var passenger in vehicle.AssignedPassengers)
                {
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

            bool allAssigned = assignedPassengers.Count == allPassengers.Count;

            // Calculate statistics
            double totalDistance = solution.Vehicles.Sum(v => v.TotalDistance);
            double totalTime = solution.Vehicles.Sum(v => v.TotalTime);
            double averageTime = totalTime / solution.Vehicles.Count(v => v.AssignedPassengers.Count > 0);
            int usedVehicles = solution.Vehicles.Count(v => v.AssignedPassengers.Count > 0);

            StringBuilder report = new StringBuilder();
            report.AppendLine("Validation Results:");
            report.AppendLine($"All passengers assigned: {allAssigned}");
            report.AppendLine($"Assigned passengers: {assignedPassengers.Count}/{allPassengers.Count}");
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
            report.AppendLine($"Used vehicles: {usedVehicles}/{solution.Vehicles.Count}");

            return report.ToString();
        }
    }
}