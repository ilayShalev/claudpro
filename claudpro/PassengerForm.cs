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

namespace claudpro
{
    public partial class PassengerForm : Form
    {
        // Fields for database and services
        private readonly DatabaseService dbService;
        private readonly MapService mapService;
        private readonly int userId;
        private readonly string username;

        // UI controls
        private GMapControl gMapControl;
        private CheckBox availabilityCheckBox;
        private RichTextBox assignmentDetailsTextBox;
        private Button refreshButton;
        private Button logoutButton;
        private Panel leftPanel;

        // Fields for location setting functionality
        private bool isSettingLocation = false;
        private Label locationInstructionsLabel;
        private TextBox addressTextBox;

        // Data models
        private Passenger passenger;
        private Vehicle assignedVehicle;
        private DateTime? pickupTime;

        public PassengerForm(DatabaseService dbService, MapService mapService, int userId, string username)
        {
            this.dbService = dbService;
            this.mapService = mapService;
            this.userId = userId;
            this.username = username;

            // Use the designer-generated InitializeComponent
            InitializeComponent();

            // Setup UI manually
            SetupUI();

            this.Load += async (s, e) => await LoadPassengerDataAsync();
        }

        private void SetupUI()
        {
            // Set form properties
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
                "I need a ride tomorrow",
                new Point(20, 50),
                new Size(300, 30),
                true
            );
            availabilityCheckBox.CheckedChanged += async (s, e) => await UpdateAvailabilityAsync();
            leftPanel.Controls.Add(availabilityCheckBox);

            var statusPanel = ControlExtensions.CreatePanel(
                new Point(20, 90),
                new Size(310, 2),
                BorderStyle.FixedSingle
            );
            statusPanel.BackColor = Color.Gray;
            leftPanel.Controls.Add(statusPanel);

            // Assignment details section
            leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Your Ride Details:",
                new Point(20, 110),
                new Size(200, 20),
                new Font("Arial", 10, FontStyle.Bold)
            ));

            assignmentDetailsTextBox = ControlExtensions.CreateRichTextBox(
                new Point(20, 140),
                new Size(310, 200),
                true
            );
            leftPanel.Controls.Add(assignmentDetailsTextBox);

            // Add location setting controls
            AddLocationSettingControls();

            // Buttons
            refreshButton = ControlExtensions.CreateButton(
                "Refresh",
                new Point(20, 530),
                new Size(150, 30),
                async (s, e) => await LoadPassengerDataAsync()
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
            mapService.InitializeGoogleMaps(gMapControl);
        }

        private void AddLocationSettingControls()
        {
            // Add a separator panel
            var locationPanel = ControlExtensions.CreatePanel(
                new Point(20, 350),
                new Size(310, 2),
                BorderStyle.FixedSingle
            );
            locationPanel.BackColor = Color.Gray;
            leftPanel.Controls.Add(locationPanel);

            // Add location setting section title
            leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Set Your Pickup Location:",
                new Point(20, 360),
                new Size(200, 20),
                new Font("Arial", 10, FontStyle.Bold)
            ));

            // Add a button for setting location
            var setLocationButton = ControlExtensions.CreateButton(
                "Set Location on Map",
                new Point(20, 390),
                new Size(150, 30),
                (s, e) => EnableMapLocationSelection()
            );
            leftPanel.Controls.Add(setLocationButton);

            // Add a search box for address
            leftPanel.Controls.Add(ControlExtensions.CreateLabel(
                "Or Search Address:",
                new Point(20, 430),
                new Size(150, 20)
            ));

            addressTextBox = ControlExtensions.CreateTextBox(
                new Point(20, 455),
                new Size(220, 25)
            );
            var searchButton = ControlExtensions.CreateButton(
                "Search",
                new Point(250, 455),
                new Size(80, 25),
                async (s, e) => await SearchAddressAsync(addressTextBox.Text)
            );

            leftPanel.Controls.Add(addressTextBox);
            leftPanel.Controls.Add(searchButton);

            // Add instructions label
            locationInstructionsLabel = ControlExtensions.CreateLabel(
                "Click on the map to set your pickup location",
                new Point(20, 490),
                new Size(310, 20),
                null,
                ContentAlignment.MiddleCenter
            );
            locationInstructionsLabel.ForeColor = Color.Red;
            locationInstructionsLabel.Visible = false;
            leftPanel.Controls.Add(locationInstructionsLabel);
        }


        private void EnableMapLocationSelection()
        {
            isSettingLocation = true;
            locationInstructionsLabel.Visible = true;

            // Change cursor to indicate map is clickable
            gMapControl.Cursor = Cursors.Hand;

            // Add event handler for map clicks
            gMapControl.MouseClick += MapClickToSetLocation;

            MessageBox.Show("Click on the map to set your pickup location",
                "Set Location", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void MapClickToSetLocation(object sender, MouseEventArgs e)
        {
            if (!isSettingLocation) return;

            // Convert clicked point to geo coordinates
            PointLatLng point = gMapControl.FromLocalToLatLng(e.X, e.Y);

            // Set passenger location
            UpdatePassengerLocation(point.Lat, point.Lng);

            // Disable location setting mode
            isSettingLocation = false;
            locationInstructionsLabel.Visible = false;
            gMapControl.Cursor = Cursors.Default;
            gMapControl.MouseClick -= MapClickToSetLocation;
        }

        private async Task SearchAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;

            try
            {
                // Show searching indicator
                Cursor = Cursors.WaitCursor;
                addressTextBox.Enabled = false;

                var result = await mapService.GeocodeAddressAsync(address);
                if (result.HasValue)
                {
                    // Center map on found location
                    gMapControl.Position = new PointLatLng(result.Value.Latitude, result.Value.Longitude);
                    gMapControl.Zoom = 15;

                    // Update passenger location
                    UpdatePassengerLocation(result.Value.Latitude, result.Value.Longitude);
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
                // Reset cursor and enable address box
                Cursor = Cursors.Default;
                addressTextBox.Enabled = true;
            }
        }

        private async void UpdatePassengerLocation(double latitude, double longitude)
        {
            try
            {
                // Show waiting cursor
                Cursor = Cursors.WaitCursor;

                // Get address from coordinates (reverse geocoding)
                string address = await mapService.ReverseGeocodeAsync(latitude, longitude);

                // Update passenger in database
                if (passenger == null)
                {
                    passenger = new Passenger
                    {
                        UserId = userId,
                        Name = username,
                        Latitude = latitude,
                        Longitude = longitude,
                        Address = address,
                        IsAvailableTomorrow = availabilityCheckBox.Checked
                    };

                    int passengerId = await dbService.AddPassengerAsync(userId, username, latitude, longitude, address);
                    passenger.Id = passengerId;
                }
                else
                {
                    passenger.Latitude = latitude;
                    passenger.Longitude = longitude;
                    passenger.Address = address;

                    await dbService.UpdatePassengerAsync(passenger.Id, passenger.Name,
                        latitude, longitude, address);
                }

                // Show confirmation and update marker on map
                MessageBox.Show($"Your pickup location has been set to:\n{address}",
                    "Location Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DisplayPassengerOnMap();

                // Refresh passenger details
                await LoadPassengerDataAsync();
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


        // Updated UpdateAssignmentDetailsText method to display pickup time
        private void UpdateAssignmentDetailsText()
        {
            if (assignmentDetailsTextBox == null) return;

            assignmentDetailsTextBox.Clear();

            if (passenger == null)
            {
                assignmentDetailsTextBox.AppendText("No passenger profile found.\n");
                assignmentDetailsTextBox.AppendText("Please set your pickup location first.");
                return;
            }

            try
            {
                assignmentDetailsTextBox.SelectionFont = new Font(assignmentDetailsTextBox.Font, FontStyle.Bold);
                assignmentDetailsTextBox.AppendText("Your Information:\n");
                assignmentDetailsTextBox.SelectionFont = assignmentDetailsTextBox.Font;
                assignmentDetailsTextBox.AppendText($"Name: {passenger.Name}\n");

                if (!string.IsNullOrEmpty(passenger.Address))
                    assignmentDetailsTextBox.AppendText($"Pickup Location: {passenger.Address}\n\n");
                else
                    assignmentDetailsTextBox.AppendText($"Pickup Location: ({passenger.Latitude:F6}, {passenger.Longitude:F6})\n\n");

                if (assignedVehicle != null)
                {
                    assignmentDetailsTextBox.SelectionFont = new Font(assignmentDetailsTextBox.Font, FontStyle.Bold);
                    assignmentDetailsTextBox.AppendText("Your Scheduled Ride:\n");
                    assignmentDetailsTextBox.SelectionFont = assignmentDetailsTextBox.Font;

                    string driverName = !string.IsNullOrEmpty(assignedVehicle.DriverName)
                        ? assignedVehicle.DriverName
                        : $"Driver #{assignedVehicle.Id}";

                    assignmentDetailsTextBox.AppendText($"Driver: {driverName}\n");

                    // Only add vehicle details if they are available
                    if (!string.IsNullOrEmpty(assignedVehicle.Model))
                        assignmentDetailsTextBox.AppendText($"Vehicle: {assignedVehicle.Model}\n");

                    if (!string.IsNullOrEmpty(assignedVehicle.Color))
                        assignmentDetailsTextBox.AppendText($"Color: {assignedVehicle.Color}\n");

                    if (!string.IsNullOrEmpty(assignedVehicle.LicensePlate))
                        assignmentDetailsTextBox.AppendText($"License Plate: {assignedVehicle.LicensePlate}\n");

                    // Show pickup time with proper formatting using the centralized formatter
                    if (!string.IsNullOrEmpty(passenger.EstimatedPickupTime))
                    {
                        string formattedPickupTime = TimeFormatUtility.FormatTimeDisplay(passenger.EstimatedPickupTime);
                        assignmentDetailsTextBox.SelectionFont = new Font(assignmentDetailsTextBox.Font, FontStyle.Bold);
                        assignmentDetailsTextBox.AppendText($"Pickup Time: {formattedPickupTime}\n");
                        assignmentDetailsTextBox.SelectionFont = assignmentDetailsTextBox.Font;
                    }
                    // Fall back to pickupTime from database if EstimatedPickupTime is not set
                    else if (pickupTime.HasValue)
                    {
                        assignmentDetailsTextBox.SelectionFont = new Font(assignmentDetailsTextBox.Font, FontStyle.Bold);
                        assignmentDetailsTextBox.AppendText($"Pickup Time: {pickupTime.Value.ToString("HH:mm")}\n");
                        assignmentDetailsTextBox.SelectionFont = assignmentDetailsTextBox.Font;
                    }
                    else
                    {
                        assignmentDetailsTextBox.AppendText("Pickup Time: Not yet scheduled\n");
                    }

                    if (!string.IsNullOrEmpty(assignedVehicle.StartAddress))
                    {
                        assignmentDetailsTextBox.AppendText($"Driver Starting From: {assignedVehicle.StartAddress}\n");
                    }
                }
                else
                {
                    assignmentDetailsTextBox.SelectionFont = new Font(assignmentDetailsTextBox.Font, FontStyle.Bold);
                    assignmentDetailsTextBox.AppendText("No Ride Scheduled Yet\n");
                    assignmentDetailsTextBox.SelectionFont = assignmentDetailsTextBox.Font;
                    assignmentDetailsTextBox.AppendText("Rides for tomorrow will be assigned by the system overnight.\n");
                    assignmentDetailsTextBox.AppendText("Please check back tomorrow morning for your ride details.\n");
                }
            }
            catch (Exception ex)
            {
                assignmentDetailsTextBox.Clear();
                assignmentDetailsTextBox.AppendText($"Error displaying information: {ex.Message}");
            }
        }
        // Updated LoadPassengerDataAsync to get the complete passenger details with EstimatedPickupTime
        private async Task LoadPassengerDataAsync()
        {
            refreshButton.Enabled = false;
            assignmentDetailsTextBox.Clear();
            assignmentDetailsTextBox.AppendText("Loading ride details...\n");

            try
            {
                // Load passenger data
                passenger = await dbService.GetPassengerByUserIdAsync(userId);

                if (passenger != null)
                {
                    // Update UI to reflect passenger data
                    availabilityCheckBox.Checked = passenger.IsAvailableTomorrow;

                    // Try to load assigned vehicle if available
                    var assignment = await dbService.GetPassengerAssignmentAsync(userId, DateTime.Now.ToString("yyyy-MM-dd"));
                    assignedVehicle = assignment.AssignedVehicle;
                    pickupTime = assignment.PickupTime;

                    // Get the full passenger details including the estimated pickup time
                    if (passenger.Id > 0)
                    {
                        var fullPassenger = await dbService.GetPassengerByIdAsync(passenger.Id);
                        if (fullPassenger != null && !string.IsNullOrEmpty(fullPassenger.EstimatedPickupTime))
                        {
                            passenger.EstimatedPickupTime = fullPassenger.EstimatedPickupTime;
                        }
                    }

                    // Display data on map and in text
                    DisplayPassengerOnMap();
                    UpdateAssignmentDetailsText();
                }
                else
                {
                    assignmentDetailsTextBox.Clear();
                    assignmentDetailsTextBox.AppendText("No passenger profile found. Set your location to create a profile.\n");
                }
            }
            catch (Exception ex)
            {
                assignmentDetailsTextBox.Clear();
                assignmentDetailsTextBox.AppendText($"Error loading data: {ex.Message}\n");
            }
            finally
            {
                refreshButton.Enabled = true;
            }
        }

        // Updated DisplayPassengerOnMap to show routes
        private void DisplayPassengerOnMap()
        {
            if (passenger == null) return;

            gMapControl.Overlays.Clear();

            var passengersOverlay = new GMapOverlay("passengers");

            // Create passenger marker
            var marker = MapOverlays.CreatePassengerMarker(passenger);
            passengersOverlay.Markers.Add(marker);

            gMapControl.Overlays.Add(passengersOverlay);

            // Create destination marker if there's an assigned vehicle
            if (assignedVehicle != null)
            {
                // Get destination
                Task.Run(async () => {
                    var destination = await dbService.GetDestinationAsync();
                    this.Invoke(new Action(() => {
                        try
                        {
                            // Create destination marker
                            var destinationOverlay = new GMapOverlay("destination");
                            var destinationMarker = MapOverlays.CreateDestinationMarker(destination.Latitude, destination.Longitude);
                            destinationOverlay.Markers.Add(destinationMarker);
                            gMapControl.Overlays.Add(destinationOverlay);

                            gMapControl.Refresh();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error creating destination marker: {ex.Message}");
                        }
                    }));
                });
            }

            // Position map on passenger location
            gMapControl.Position = new PointLatLng(passenger.Latitude, passenger.Longitude);
            gMapControl.Zoom = 15;
            gMapControl.Refresh();
        }
        private async Task UpdateAvailabilityAsync()
        {
            if (passenger == null)
                return;

            try
            {
                bool success = await dbService.UpdatePassengerAvailabilityAsync(passenger.Id, availabilityCheckBox.Checked);

                if (success)
                {
                    passenger.IsAvailableTomorrow = availabilityCheckBox.Checked;

                    // If now available and not assigned, show message
                    if (passenger.IsAvailableTomorrow && assignedVehicle == null)
                    {
                        MessageBox.Show("Your ride request has been submitted. A driver will be assigned soon.",
                            "Status Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    // Refresh UI to reflect change
                    UpdateAssignmentDetailsText();
                }
                else
                {
                    MessageBox.Show("Failed to update availability. Please try again.",
                        "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    // Revert checkbox to match database state
                    availabilityCheckBox.CheckedChanged -= async (s, e) => await UpdateAvailabilityAsync();
                    availabilityCheckBox.Checked = passenger.IsAvailableTomorrow;
                    availabilityCheckBox.CheckedChanged += async (s, e) => await UpdateAvailabilityAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating availability: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Revert checkbox to match database state
                availabilityCheckBox.CheckedChanged -= async (s, e) => await UpdateAvailabilityAsync();
                availabilityCheckBox.Checked = passenger.IsAvailableTomorrow;
                availabilityCheckBox.CheckedChanged += async (s, e) => await UpdateAvailabilityAsync();
            }
        }

        private void PassengerForm_Load(object sender, EventArgs e)
        {
            // Initialization code for PassengerForm
            try
            {
                if (gMapControl != null)
                {
                    gMapControl.Position = new PointLatLng(32.0741, 34.7922); // Default to Tel Aviv
                    gMapControl.Zoom = 12;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing map: {ex.Message}", "Initialization Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}