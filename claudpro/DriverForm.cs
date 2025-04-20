using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using claudpro.Models;
using claudpro.Services;
using claudpro.UI;
using System.Linq;


namespace claudpro
{
    public partial class DriverForm : Form
    {
        // Fields for database and services
        private readonly DatabaseService dbService;
        private readonly MapService mapService;
        private readonly int userId;
        private readonly string username;

        // UI controls
        private GMapControl gMapControl;
        private CheckBox availabilityCheckBox;
        private RichTextBox routeDetailsTextBox;
        private Button refreshButton;
        private Button logoutButton;
        private Panel leftPanel;

        // Fields for location setting functionality
        private bool isSettingLocation = false;
        private Label locationInstructionsLabel;
        private AddressSearchControl addressSearchControl;
        private Button setLocationButton;
        
        // Fields for vehicle capacity
        private NumericUpDown capacityNumericUpDown;
        private Button updateCapacityButton;

        // Data models
        private Vehicle vehicle;
        private List<Passenger> assignedPassengers = new List<Passenger>();
        private DateTime? pickupTime;

        public DriverForm(DatabaseService dbService, MapService mapService, int userId, string username)
        {
            this.dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
            this.mapService = mapService ?? throw new ArgumentNullException(nameof(mapService));
            this.userId = userId;
            this.username = username ?? throw new ArgumentNullException(nameof(username));

            // Use the designer-generated InitializeComponent
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                // Setup UI manually
                SetupUI();

                // Initialize map with default position
                if (gMapControl != null)
                {
                    mapService.InitializeGoogleMaps(gMapControl, 32.0741, 34.7922);
                }

                // Load data asynchronously
                LoadDriverDataAsync().ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                    {
                        this.Invoke(new Action(() =>
                        {
                            MessageBox.Show($"Error loading driver data: {t.Exception.InnerException?.Message ?? t.Exception.Message}",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }));
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing driver form: {ex.Message}",
                    "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetupUI()
        {
            try
            {
                // Set form properties - this can be moved to the designer
                this.Text = "RideMatch - Driver Interface";
                this.Size = new Size(1000, 700);
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.StartPosition = FormStartPosition.CenterScreen;

                // Title
                var titleLabel = ControlExtensions.CreateLabel(
                    $"Welcome, {username}",
                    new Point(20, 20),
                    new Size(960, 30),
                    new Font("Arial", 16, FontStyle.Bold),
                    ContentAlignment.MiddleCenter
                );
                Controls.Add(titleLabel);

                // Left panel for controls and details
                leftPanel = ControlExtensions.CreatePanel(
                    new Point(20, 70),
                    new Size(350, 580),
                    BorderStyle.FixedSingle
                );
                Controls.Add(leftPanel);

                // Availability controls
                leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                    "Tomorrow's Status:",
                    new Point(20, 20),
                    new Size(150, 20),
                    new Font("Arial", 10, FontStyle.Bold)
                ));

                availabilityCheckBox = ControlExtensions.CreateCheckBox(
                    "I am available to drive tomorrow",
                    new Point(20, 50),
                    new Size(300, 30),
                    true
                );
                availabilityCheckBox.CheckedChanged += async (s, e) => await UpdateAvailabilityAsync();
                leftPanel.Controls.Add(availabilityCheckBox);

                // Vehicle capacity section
                leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                    "Vehicle Capacity:",
                    new Point(20, 90),
                    new Size(150, 20),
                    new Font("Arial", 10, FontStyle.Bold)
                ));
                
                leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                    "Number of seats:", 
                    new Point(20, 120), 
                    new Size(150, 20)
                ));
                
                capacityNumericUpDown = new NumericUpDown
                {
                    Location = new Point(180, 120),
                    Size = new Size(80, 25),
                    Minimum = 1,
                    Maximum = 20,
                    Value = 4 // Default 4 seats
                };
                leftPanel.Controls.Add(capacityNumericUpDown);
                
                updateCapacityButton = ControlExtensions.CreateButton(
                    "Update Capacity",
                    new Point(180, 150),
                    new Size(150, 30),
                    async (s, e) => await UpdateVehicleCapacityAsync()
                );
                leftPanel.Controls.Add(updateCapacityButton);

                var statusPanel = ControlExtensions.CreatePanel(
                    new Point(20, 190),
                    new Size(310, 2),
                    BorderStyle.FixedSingle
                );
                statusPanel.BackColor = Color.Gray;
                leftPanel.Controls.Add(statusPanel);

                // Route details section
                leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                    "Your Route Details:",
                    new Point(20, 210),
                    new Size(200, 20),
                    new Font("Arial", 10, FontStyle.Bold)
                ));

                routeDetailsTextBox = ControlExtensions.CreateRichTextBox(
                    new Point(20, 240),
                    new Size(310, 160),
                    true
                );
                leftPanel.Controls.Add(routeDetailsTextBox);

                // Add location setting functionality
                AddLocationSettingControls();

                // Buttons
                refreshButton = ControlExtensions.CreateButton(
                    "Refresh",
                    new Point(20, 530),
                    new Size(150, 30),
                    async (s, e) => await LoadDriverDataAsync()
                );
                leftPanel.Controls.Add(refreshButton);

                logoutButton = ControlExtensions.CreateButton(
                    "Logout",
                    new Point(180, 530),
                    new Size(150, 30),
                    (s, e) => Close()
                );
                leftPanel.Controls.Add(logoutButton);

                // Map
                gMapControl = new GMapControl
                {
                    Location = new Point(390, 70),
                    Size = new Size(580, 580),
                    MinZoom = 2,
                    MaxZoom = 18,
                    Zoom = 13,
                    DragButton = MouseButtons.Left
                };
                Controls.Add(gMapControl);

                // Initialize message
                routeDetailsTextBox.AppendText("Loading driver data...\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting up UI: {ex.Message}",
                    "UI Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddLocationSettingControls()
        {
            try
            {
                // Add a separator panel
                var locationPanel = ControlExtensions.CreatePanel(
                    new Point(20, 410),
                    new Size(310, 2),
                    BorderStyle.FixedSingle
                );
                locationPanel.BackColor = Color.Gray;
                leftPanel.Controls.Add(locationPanel);

                // Add location setting section title
                leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                    "Set Your Starting Location:",
                    new Point(20, 420),
                    new Size(200, 20),
                    new Font("Arial", 10, FontStyle.Bold)
                ));

                // Add the "Set Location on Map" button
                var setLocationButton = ControlExtensions.CreateButton(
                    "Set Location on Map",
                    new Point(20, 445),
                    new Size(150, 30),
                    (s, e) => EnableMapLocationSelection()
                );
                leftPanel.Controls.Add(setLocationButton);

                // Add the "Or Search Address:" label
                leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                    "Or Search Address:",
                    new Point(20, 472),
                    new Size(150, 20)
                ));

                // Add address textbox
                var addressTextBox = ControlExtensions.CreateTextBox(
                    new Point(20, 500),
                    new Size(220, 25)
                );
                leftPanel.Controls.Add(addressTextBox);

                // Add search button
                var searchButton = ControlExtensions.CreateButton(
                    "Search",
                    new Point(250, 500),
                    new Size(80, 25),
                    async (s, e) => await SearchAddressAsync(addressTextBox.Text)
                );
                leftPanel.Controls.Add(searchButton);

                // Add instructions label (hidden initially)
                locationInstructionsLabel = ControlExtensions.CreateLabel(
                    "Click on the map to set your starting location",
                    new Point(20, 550),
                    new Size(310, 20),
                    null,
                    ContentAlignment.MiddleCenter
                );
                locationInstructionsLabel.ForeColor = Color.Red;
                locationInstructionsLabel.Visible = false;
                leftPanel.Controls.Add(locationInstructionsLabel);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting up location controls: {ex.Message}",
                    "UI Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Add this method to handle address search
        private async Task SearchAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;

            try
            {
                // Show searching indicator
                Cursor = Cursors.WaitCursor;

                var result = await mapService.GeocodeAddressAsync(address);
                if (result.HasValue)
                {
                    // Center map on found location
                    gMapControl.Position = new PointLatLng(result.Value.Latitude, result.Value.Longitude);
                    gMapControl.Zoom = 15;

                    // Update vehicle location
                    UpdateVehicleLocation(result.Value.Latitude, result.Value.Longitude);
                }
                else
                {
                    MessageBox.Show("Address not found. Please try again.", "Search Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Reset cursor
                Cursor = Cursors.Default;
            }
        }
        private void AddressSearchControl_AddressFound(object sender, AddressFoundEventArgs e)
        {
            try
            {
                // When an address is found by the AddressSearchControl, update the vehicle location
                UpdateVehicleLocation(e.Latitude, e.Longitude, e.FormattedAddress);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing address: {ex.Message}",
                    "Address Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadDriverDataAsync()
        {
            if (refreshButton == null || routeDetailsTextBox == null) return;

            refreshButton.Enabled = false;
            routeDetailsTextBox.Clear();
            routeDetailsTextBox.AppendText("Loading route data...\n");

            try
            {
                // Load vehicle and route data
                vehicle = await dbService.GetVehicleByUserIdAsync(userId);

                if (vehicle == null)
                {
                    routeDetailsTextBox.Clear();
                    routeDetailsTextBox.AppendText("No vehicle is assigned to you.\n");
                    routeDetailsTextBox.AppendText("Please set your vehicle information.\n");

                    // Initialize with default values
                    vehicle = new Vehicle
                    {
                        UserId = userId,
                        Capacity = 4,
                        IsAvailableTomorrow = true,
                        DriverName = username
                    };

                    return;
                }

                // Update UI controls to reflect vehicle data
                availabilityCheckBox.Checked = vehicle.IsAvailableTomorrow;
                capacityNumericUpDown.Value = vehicle.Capacity;

                var destination = await dbService.GetDestinationAsync();

                // Get today's date in the format used by the database
                TimeSpan timeToAdd = TimeSpan.Parse(destination.TargetTime);
                DateTime now = DateTime.Now;
                DateTime targetTime = new DateTime(now.Year, now.Month, now.Day,
                                                         timeToAdd.Hours, timeToAdd.Minutes, timeToAdd.Seconds);
                string queryTime = now.ToString("yyyy-MM-dd");
                if (targetTime < DateTime.Now)
                {
                    queryTime = now.AddDays(1).ToString("yyyy-MM-dd");
                }

                // Get route data
                var routeData = await dbService.GetDriverRouteAsync(userId, queryTime);

                // Debug output - remove in production
                Console.WriteLine($"Retrieved vehicle - Departure time: {routeData.Vehicle?.DepartureTime ?? "null"}");

                if (routeData.Vehicle != null)
                {
                    // Important: Make sure to update the vehicle with data from routeData
                    // This ensures we get the most up-to-date data including departure time
                    vehicle = routeData.Vehicle;
                }

                assignedPassengers = routeData.Passengers ?? new List<Passenger>();

                // Debug output - remove in production
                if (assignedPassengers != null)
                {
                    foreach (var p in assignedPassengers)
                    {
                        Console.WriteLine($"Passenger {p.Name} - Pickup time: {p.EstimatedPickupTime ?? "null"}");
                    }
                }

                pickupTime = routeData.PickupTime;

                // Additional check for departure time from database if it's still null
                if (string.IsNullOrEmpty(vehicle.DepartureTime) && assignedPassengers != null && assignedPassengers.Count > 0)
                {
                    // If we have passengers with pickup times but no departure time,
                    // we can calculate backward from the first pickup time
                    var firstPassenger = assignedPassengers.FirstOrDefault();
                    if (firstPassenger != null && !string.IsNullOrEmpty(firstPassenger.EstimatedPickupTime))
                    {
                        if (DateTime.TryParse(firstPassenger.EstimatedPickupTime, out DateTime firstPickup))
                        {
                            // Estimate departure time as 10 minutes before first pickup
                            DateTime estimatedDeparture = firstPickup.AddMinutes(-10);
                            vehicle.DepartureTime = estimatedDeparture.ToString("HH:mm");
                            Console.WriteLine($"Estimated departure time: {vehicle.DepartureTime}");
                        }
                    }
                }

                // Clear map and display the route
                ShowRouteOnMap();

                // Update route details text
                UpdateRouteDetailsText(pickupTime);

                // Update the address search control with current address
                if (addressSearchControl != null && !string.IsNullOrEmpty(vehicle.StartAddress))
                {
                    addressSearchControl.Address = vehicle.StartAddress;
                }
            }
            catch (Exception ex)
            {
                routeDetailsTextBox.Clear();
                routeDetailsTextBox.AppendText($"Error loading data: {ex.Message}\n");

                // Debug output - remove in production
                Console.WriteLine($"Error loading driver data: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                refreshButton.Enabled = true;
            }
        }
        private void DisplayVehicleAndRouteOnMap()
        {
            if (vehicle == null) return;

            gMapControl.Overlays.Clear();

            var passengersOverlay = new GMapOverlay("passengers");
            var vehiclesOverlay = new GMapOverlay("vehicles");
            var routesOverlay = new GMapOverlay("routes");
            var destinationOverlay = new GMapOverlay("destination");

            // Add vehicle marker
            var vehicleMarker = MapOverlays.CreateVehicleMarker(vehicle);
            vehiclesOverlay.Markers.Add(vehicleMarker);

            // Add passenger markers for assigned passengers
            if (vehicle.AssignedPassengers != null)
            {
                foreach (var p in vehicle.AssignedPassengers)
                {
                    var marker = MapOverlays.CreatePassengerMarker(p);
                    passengersOverlay.Markers.Add(marker);
                }
            }

            // Add the overlays to the map
            gMapControl.Overlays.Add(routesOverlay);
            gMapControl.Overlays.Add(passengersOverlay);
            gMapControl.Overlays.Add(vehiclesOverlay);
            gMapControl.Overlays.Add(destinationOverlay);

            // Position map on vehicle location
            gMapControl.Position = new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude);
            gMapControl.Zoom = 12;
        }        // Updated ShowRouteOnMap method to properly display routes
        private void ShowRouteOnMap()
        {
            if (gMapControl == null || vehicle == null) return;

            try
            {
                gMapControl.Overlays.Clear();

                var vehiclesOverlay = new GMapOverlay("vehicles");
                var passengersOverlay = new GMapOverlay("passengers");
                var routesOverlay = new GMapOverlay("routes");
                var destinationOverlay = new GMapOverlay("destination");

                // Show vehicle marker if location is set
                if (vehicle.StartLatitude != 0 || vehicle.StartLongitude != 0)
                {
                    var vehicleMarker = MapOverlays.CreateVehicleMarker(vehicle);
                    vehiclesOverlay.Markers.Add(vehicleMarker);

                    // Center map on vehicle location
                    gMapControl.Position = new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude);
                    gMapControl.Zoom = 12;
                }

                // Show passenger markers and collect route points
                List<PointLatLng> routePoints = new List<PointLatLng>();

                // Add vehicle starting point
                routePoints.Add(new PointLatLng(vehicle.StartLatitude, vehicle.StartLongitude));

                if (assignedPassengers != null && assignedPassengers.Count > 0)
                {
                    foreach (var passenger in assignedPassengers)
                    {
                        if (passenger != null)
                        {
                            var passengerMarker = MapOverlays.CreatePassengerMarker(passenger);
                            passengersOverlay.Markers.Add(passengerMarker);

                            // Add passenger location to route points
                            routePoints.Add(new PointLatLng(passenger.Latitude, passenger.Longitude));
                        }
                    }
                }

                // Get destination from database asynchronously
                Task.Run(async () => {
                    try
                    {
                        var destination = await dbService.GetDestinationAsync();

                        this.Invoke(new Action(() => {
                            try
                            {
                                // Add destination marker
                                var destMarker = MapOverlays.CreateDestinationMarker(
                                    destination.Latitude, destination.Longitude);
                                destinationOverlay.Markers.Add(destMarker);

                                // Add destination to route points
                                routePoints.Add(new PointLatLng(destination.Latitude, destination.Longitude));

                                // Try to get the Google Directions route if there are enough points
                                if (routePoints.Count >= 2)
                                {
                                    // Try to get actual route from Google
                                    Task.Run(async () => {
                                        try
                                        {
                                            var googlePoints = await mapService.GetGoogleDirectionsAsync(routePoints);

                                            this.Invoke(new Action(() => {
                                                if (googlePoints != null && googlePoints.Count > 0)
                                                {
                                                    // Create route with the Google directions
                                                    var route = MapOverlays.CreateRoute(googlePoints, "DriverRoute", Color.Blue);
                                                    routesOverlay.Routes.Add(route);
                                                }
                                                else
                                                {
                                                    // Fallback to straight line if Google directions fail
                                                    var route = MapOverlays.CreateRoute(routePoints, "DriverRoute", Color.Blue);
                                                    routesOverlay.Routes.Add(route);
                                                }

                                                // Add overlays to map
                                                gMapControl.Overlays.Add(routesOverlay);
                                                gMapControl.Overlays.Add(vehiclesOverlay);
                                                gMapControl.Overlays.Add(passengersOverlay);
                                                gMapControl.Overlays.Add(destinationOverlay);

                                                gMapControl.Refresh();
                                            }));
                                        }
                                        catch
                                        {
                                            // Fallback to straight line if Google directions throw an error
                                            this.Invoke(new Action(() => {
                                                var route = MapOverlays.CreateRoute(routePoints, "DriverRoute", Color.Blue);
                                                routesOverlay.Routes.Add(route);

                                                // Add overlays to map
                                                gMapControl.Overlays.Add(routesOverlay);
                                                gMapControl.Overlays.Add(vehiclesOverlay);
                                                gMapControl.Overlays.Add(passengersOverlay);
                                                gMapControl.Overlays.Add(destinationOverlay);

                                                gMapControl.Refresh();
                                            }));
                                        }
                                    });
                                }
                                else
                                {
                                    // Add overlays to map
                                    gMapControl.Overlays.Add(routesOverlay);
                                    gMapControl.Overlays.Add(vehiclesOverlay);
                                    gMapControl.Overlays.Add(passengersOverlay);
                                    gMapControl.Overlays.Add(destinationOverlay);

                                    gMapControl.Refresh();
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error adding destination marker: {ex.Message}");
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error getting destination: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying route: {ex.Message}",
                    "Map Display Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        // Updated UpdateRouteDetailsText to show departure time
        private void UpdateRouteDetailsText(DateTime? pickupTime)
        {
            if (routeDetailsTextBox == null) return;

            routeDetailsTextBox.Clear();

            if (vehicle == null)
            {
                routeDetailsTextBox.AppendText("No vehicle assigned.\n");
                return;
            }

            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText("Your Vehicle Details:\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
            routeDetailsTextBox.AppendText($"Capacity: {vehicle.Capacity} seats\n");

            if (vehicle.StartLatitude == 0 && vehicle.StartLongitude == 0)
            {
                routeDetailsTextBox.AppendText("Starting Location: Not set\n\n");
                routeDetailsTextBox.AppendText("Please set your starting location using the options below.\n");
            }
            else if (!string.IsNullOrEmpty(vehicle.StartAddress))
            {
                routeDetailsTextBox.AppendText($"Starting Location: {vehicle.StartAddress}\n");
            }
            else
            {
                routeDetailsTextBox.AppendText($"Starting Location: ({vehicle.StartLatitude:F6}, {vehicle.StartLongitude:F6})\n");
            }

            // Display departure time with proper formatting and handling
            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText("Departure Time: ");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;

            if (!string.IsNullOrEmpty(vehicle.DepartureTime))
            {
                string formattedDepartureTime;
                if (DateTime.TryParse(vehicle.DepartureTime, out DateTime dt))
                {
                    formattedDepartureTime = dt.ToString("HH:mm");
                }
                else
                {
                    formattedDepartureTime = vehicle.DepartureTime;
                }
                routeDetailsTextBox.AppendText(formattedDepartureTime + "\n");
            }
            else if (assignedPassengers != null && assignedPassengers.Count > 0)
            {
                // If we have assigned passengers but no departure time, show "Calculating..."
                routeDetailsTextBox.AppendText("Calculating...\n");
            }
            else
            {
                routeDetailsTextBox.AppendText("Not yet scheduled\n");
            }

            if (assignedPassengers != null && assignedPassengers.Count > 0)
            {
                routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                routeDetailsTextBox.AppendText("\nAssigned Passengers:\n");
                routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;

                for (int i = 0; i < assignedPassengers.Count; i++)
                {
                    var passenger = assignedPassengers[i];
                    if (passenger == null) continue;

                    routeDetailsTextBox.AppendText($"{i + 1}. {passenger.Name}\n");

                    if (!string.IsNullOrEmpty(passenger.Address))
                        routeDetailsTextBox.AppendText($"   Pick-up: {passenger.Address}\n");
                    else
                        routeDetailsTextBox.AppendText($"   Pick-up: ({passenger.Latitude:F6}, {passenger.Longitude:F6})\n");

                    // Display pickup time with formatting
                    if (!string.IsNullOrEmpty(passenger.EstimatedPickupTime))
                    {
                        string formattedPickupTime;
                        if (DateTime.TryParse(passenger.EstimatedPickupTime, out DateTime pt))
                        {
                            formattedPickupTime = pt.ToString("HH:mm");
                        }
                        else
                        {
                            formattedPickupTime = passenger.EstimatedPickupTime;
                        }
                        routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                        routeDetailsTextBox.AppendText($"   Pick-up Time: {formattedPickupTime}\n");
                        routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
                    }
                    else if (i == 0 && pickupTime.HasValue)
                    {
                        // For first passenger, use the pickupTime if no estimated time
                        routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                        routeDetailsTextBox.AppendText($"   Pick-up Time: {pickupTime.Value.ToString("HH:mm")}\n");
                        routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
                    }
                    else
                    {
                        routeDetailsTextBox.AppendText("   Pick-up Time: Not yet scheduled\n");
                    }

                    routeDetailsTextBox.AppendText("\n");
                }
            }
            else
            {
                routeDetailsTextBox.AppendText("\nNo passengers assigned for today's route.\n");
            }
        }
        private async Task UpdateAvailabilityAsync()
        {
            if (vehicle == null || availabilityCheckBox == null)
                return;

            try
            {
                bool success = await dbService.UpdateVehicleAvailabilityAsync(vehicle.Id, availabilityCheckBox.Checked);

                if (success)
                {
                    vehicle.IsAvailableTomorrow = availabilityCheckBox.Checked;

                    if (vehicle.IsAvailableTomorrow)
                    {
                        MessageBox.Show("You are now marked as available to drive tomorrow.",
                            "Availability Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("You are now marked as unavailable to drive tomorrow.",
                            "Availability Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Failed to update availability. Please try again.",
                        "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    // Revert checkbox to match database state
                    availabilityCheckBox.CheckedChanged -= async (s, e) => await UpdateAvailabilityAsync();
                    availabilityCheckBox.Checked = vehicle.IsAvailableTomorrow;
                    availabilityCheckBox.CheckedChanged += async (s, e) => await UpdateAvailabilityAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating availability: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Revert checkbox to match database state
                availabilityCheckBox.CheckedChanged -= async (s, e) => await UpdateAvailabilityAsync();
                availabilityCheckBox.Checked = vehicle.IsAvailableTomorrow;
                availabilityCheckBox.CheckedChanged += async (s, e) => await UpdateAvailabilityAsync();
            }
        }
        
        private async Task UpdateVehicleCapacityAsync()
        {
            if (vehicle == null || capacityNumericUpDown == null)
                return;
                
            try
            {
                int newCapacity = (int)capacityNumericUpDown.Value;
                
                bool success = await dbService.UpdateVehicleCapacityAsync(userId, newCapacity);
                
                if (success)
                {
                    vehicle.Capacity = newCapacity;
                    MessageBox.Show($"Vehicle capacity updated to {newCapacity} seats.",
                        "Capacity Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // Refresh route details to show the updated capacity
                    UpdateRouteDetailsText(pickupTime);
                }
                else
                {
                    MessageBox.Show("Failed to update vehicle capacity. Please try again.",
                        "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        
                    // Revert numeric control to match database state
                    capacityNumericUpDown.Value = vehicle.Capacity;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating vehicle capacity: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    
                // Revert numeric control to match database state
                capacityNumericUpDown.Value = vehicle.Capacity;
            }
        }

        /// <summary>
        /// Enables map location selection mode
        /// </summary>
        private void EnableMapLocationSelection()
        {
            try
            {
                isSettingLocation = true;

                if (locationInstructionsLabel != null)
                    locationInstructionsLabel.Visible = true;

                // Change cursor to indicate map is clickable
                if (gMapControl != null)
                {
                    gMapControl.Cursor = Cursors.Hand;
                    gMapControl.MouseClick += GMapControl_MouseClick;
                }

                MessageBox.Show("Click on the map to set your starting location",
                    "Set Location", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error enabling location selection: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                isSettingLocation = false;
            }
        }

        /// <summary>
        /// Handles map click events to set location
        /// </summary>
        private void GMapControl_MouseClick(object sender, MouseEventArgs e)
        {
            if (!isSettingLocation) return;

            try
            {
                // Convert clicked point to geo coordinates
                PointLatLng point = gMapControl.FromLocalToLatLng(e.X, e.Y);

                // Get address for the clicked location
                Task.Run(async () =>
                {
                    try
                    {
                        string address = await mapService.ReverseGeocodeAsync(point.Lat, point.Lng);

                        // Update vehicle location
                        this.Invoke(new Action(() =>
                        {
                            UpdateVehicleLocation(point.Lat, point.Lng, address);
                            isSettingLocation = false;
                            locationInstructionsLabel.Visible = false;
                            setLocationButton.Visible = true;
                            gMapControl.Cursor = Cursors.Default;

                            // Remove the click handler
                            gMapControl.MouseClick -= GMapControl_MouseClick;
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.Invoke(new Action(() =>
                        {
                            MessageBox.Show($"Error getting address: {ex.Message}",
                                "Geocoding Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            isSettingLocation = false;
                            locationInstructionsLabel.Visible = false;
                            setLocationButton.Visible = true;
                            gMapControl.Cursor = Cursors.Default;

                            // Remove the click handler
                            gMapControl.MouseClick -= GMapControl_MouseClick;
                        }));
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing map click: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                isSettingLocation = false;
                locationInstructionsLabel.Visible = false;
                setLocationButton.Visible = true;
                gMapControl.Cursor = Cursors.Default;

                // Remove the click handler
                gMapControl.MouseClick -= GMapControl_MouseClick;
            }
        }

        /// <summary>
        /// Updates the vehicle location in the database and UI
        /// </summary>
        private async void UpdateVehicleLocation(double latitude, double longitude, string address = null)
        {
            try
            {
                // Show waiting cursor
                Cursor = Cursors.WaitCursor;

                // Attempt to get address if not provided
                if (string.IsNullOrEmpty(address))
                {
                    address = await mapService.ReverseGeocodeAsync(latitude, longitude);
                }

                // Update vehicle in database
                bool success;
                
                if (vehicle == null || vehicle.Id == 0)
                {
                    // Create a new vehicle
                    int vehicleId = await dbService.SaveDriverVehicleAsync(
                        userId, 
                        capacityNumericUpDown != null ? (int)capacityNumericUpDown.Value : 4,
                        latitude,
                        longitude,
                        address
                    );
                    
                    success = vehicleId > 0;
                    
                    if (success)
                    {
                        // Load the newly created vehicle
                        vehicle = await dbService.GetVehicleByUserIdAsync(userId);
                    }
                }
                else
                {
                    // Update existing vehicle
                    success = await dbService.UpdateVehicleLocationAsync(
                        userId,
                        latitude,
                        longitude,
                        address
                    );
                    
                    if (success)
                    {
                        // Update local vehicle data
                        vehicle.StartLatitude = latitude;
                        vehicle.StartLongitude = longitude;
                        vehicle.StartAddress = address;
                    }
                }

                if (success)
                {
                    // Update address in search control
                    if (addressSearchControl != null)
                        addressSearchControl.Address = address;

                    // Show confirmation and update marker on map
                    MessageBox.Show($"Your starting location has been set to:\n{address}",
                        "Location Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Refresh map display
                    ShowRouteOnMap();

                    // Refresh vehicle details
                    UpdateRouteDetailsText(pickupTime);
                }
                else
                {
                    MessageBox.Show("Failed to update location. Please try again.",
                        "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating location: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Reset cursor
                Cursor = Cursors.Default;
            }
        }

        private void DriverForm_Load(object sender, EventArgs e)
        {

        }
    }
}