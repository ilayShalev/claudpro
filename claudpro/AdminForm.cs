using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SQLite;
using claudpro.Models;
using claudpro.Services;
using claudpro.UI;
using GMap.NET.WindowsForms;

namespace claudpro
{
    public partial class AdminForm : Form
    {
        private readonly DatabaseService dbService;
        private readonly MapService mapService;
        private RoutingService routingService;
        private RideSharingGenetic algorithmService;

        private TabControl tabControl;
        private TabPage usersTab;
        private TabPage driverTab; // Changed from driversTab to match usage
        private TabPage passengersTab;
        private TabPage routesTab;
        private TabPage destinationTab;
        private TabPage schedulingTab;

        // Lists to store data
        private List<(int Id, string Username, string UserType, string Name, string Email, string Phone)> users;
        private List<Vehicle> vehicles;
        private List<Passenger> passengers;
        private Solution currentSolution;
        private ListView usersListView;
        private ListView driversListView; // Added missing ListView

        // Destination information
        private double destinationLat;
        private double destinationLng;
        private string destinationName;
        private string destinationAddress;
        private string destinationTargetTime;

        // Route details text box
        private RichTextBox routeDetailsTextBox;

        public AdminForm(DatabaseService dbService, MapService mapService)
        {
            this.dbService = dbService;
            this.mapService = mapService;

            // Use the designer-generated InitializeComponent
            InitializeComponent();

            // Call our custom UI setup
            SetupUI();

            // Load destination
            Task.Run(async () => {
                var dest = await dbService.GetDestinationAsync();
                destinationLat = dest.Latitude;
                destinationLng = dest.Longitude;
                destinationName = dest.Name;
                destinationAddress = dest.Address;
                destinationTargetTime = dest.TargetTime;

                // Initialize routing and algorithm services
                routingService = new RoutingService(mapService, destinationLat, destinationLng, destinationTargetTime);

                // Load data on form load
                await LoadAllDataAsync();
            });
        }

        private void SetupUI()
        {
            // Remove this if you move it to the Designer
            Text = "RideMatch - Administrator Interface";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;

            tabControl = new TabControl
            {
                Location = new Point(10, 10),
                Size = new Size(1170, 740),
                Dock = DockStyle.Fill
            };
            Controls.Add(tabControl);

            // Create tabs
            usersTab = new TabPage("Users");
            driverTab = new TabPage("Drivers"); // Changed from driversTab to match usage
            passengersTab = new TabPage("Passengers");
            routesTab = new TabPage("Routes");
            destinationTab = new TabPage("Destination");
            schedulingTab = new TabPage("Scheduling");

            // Add tabs to tab control
            tabControl.TabPages.Add(usersTab);
            tabControl.TabPages.Add(driverTab);
            tabControl.TabPages.Add(passengersTab);
            tabControl.TabPages.Add(routesTab);
            tabControl.TabPages.Add(destinationTab);
            tabControl.TabPages.Add(schedulingTab);

            // Setup each tab
            SetupUsersTab();
            SetupDriverTab();
            SetupPassengersTab();
            SetupRoutesTab();
            SetupDestinationTab();
            SetupSchedulingTab();
        }

        #region Tab Setup

        private void SetupUsersTab()
        {

            // Users ListView
            usersListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(1140, 650),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            usersListView.Columns.Add("ID", 50);
            usersListView.Columns.Add("Username", 150);
            usersListView.Columns.Add("User Type", 100);
            usersListView.Columns.Add("Name", 200);
            usersListView.Columns.Add("Email", 200);
            usersListView.Columns.Add("Phone", 150);
            usersTab.Controls.Add(usersListView);

            // Refresh button
            var refreshButton = ControlExtensions.CreateButton(
                "Refresh Users",
                new Point(10, 10),
                new Size(120, 30),
                async (s, e) => {
                    await LoadUsersAsync();
                    await DisplayUsersAsync(usersListView);
                }
            );
            usersTab.Controls.Add(refreshButton);

            // Add User button
            var addUserButton = ControlExtensions.CreateButton(
                "Add User",
                new Point(140, 10),
                new Size(120, 30),
                (s, e) => {
                    using (var regForm = new RegistrationForm(dbService))
                    {
                        if (regForm.ShowDialog() == DialogResult.OK)
                        {
                            LoadUsersAsync().ContinueWith(_ => {
                                this.Invoke(new Action(async () => {
                                    await DisplayUsersAsync(usersListView);
                                }));
                            });
                        }
                    }
                }
            );
            usersTab.Controls.Add(addUserButton);

            // Edit User button
            var editUserButton = ControlExtensions.CreateButton(
                "Edit User",
                new Point(270, 10),
                new Size(120, 30),
                (s, e) => {
                    // Get the selected user
                    if (usersListView.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Please select a user to edit.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int userId = int.Parse(usersListView.SelectedItems[0].Text);
                    string username = usersListView.SelectedItems[0].SubItems[1].Text;
                    string userType = usersListView.SelectedItems[0].SubItems[2].Text;
                    string name = usersListView.SelectedItems[0].SubItems[3].Text;
                    string email = usersListView.SelectedItems[0].SubItems[4].Text;
                    string phone = usersListView.SelectedItems[0].SubItems[5].Text;

                    // Display an edit dialog
                    using (var editForm = new UserEditForm(dbService, userId, username, userType, name, email, phone))
                    {
                        if (editForm.ShowDialog() == DialogResult.OK)
                        {
                            // Refresh user list
                            LoadUsersAsync().ContinueWith(_ => {
                                this.Invoke(new Action(async () => {
                                    await DisplayUsersAsync(usersListView);
                                }));
                            });
                        }
                    }
                }
            );
            usersTab.Controls.Add(editUserButton);

            // Delete User button
            var deleteUserButton = ControlExtensions.CreateButton(
                "Delete User",
                new Point(400, 10),
                new Size(120, 30),
                async (s, e) => {
                    // Get the selected user
                    if (usersListView.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Please select a user to delete.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int userId = int.Parse(usersListView.SelectedItems[0].Text);
                    string username = usersListView.SelectedItems[0].SubItems[1].Text;

                    // Confirm deletion
                    if (MessageBox.Show($"Are you sure you want to delete user {username}?",
                            "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            // Delete the user
                            bool success = await dbService.DeleteUserAsync(userId);

                            if (success)
                            {
                                // Refresh user list
                                await LoadUsersAsync();
                                await DisplayUsersAsync(usersListView);
                                MessageBox.Show($"User {username} deleted successfully.",
                                    "User Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show($"Could not delete user {username}. The user may not exist.",
                                    "Deletion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error deleting user: {ex.Message}",
                                "Deletion Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            );
            usersTab.Controls.Add(deleteUserButton);
        }

        private void SetupDriverTab()
        {
            // Initialize driversListView
            driversListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(1140, 400),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            // Change ListView column headers
            driversListView.Columns.Add("ID", 50);
            driversListView.Columns.Add("Driver Name", 150);
            driversListView.Columns.Add("Vehicle Capacity", 80);
            driversListView.Columns.Add("Start Location", 300);
            driversListView.Columns.Add("Available Tomorrow", 120);
            driversListView.Columns.Add("User ID", 80);

            // Add the ListView to the tab
            driverTab.Controls.Add(driversListView);

            // Change button text
            var refreshButton = ControlExtensions.CreateButton(
                "Refresh Drivers",
                new Point(10, 10),
                new Size(120, 30),
                async (s, e) => {
                    await LoadVehiclesAsync();
                    await DisplayDriversAsync(driversListView);
                }
            );
            driverTab.Controls.Add(refreshButton);

            var addDriverButton = ControlExtensions.CreateButton(
                "Add Driver",
                new Point(140, 10),
                new Size(120, 30),
                (s, e) => ShowVehicleEditForm(0)
            );
            driverTab.Controls.Add(addDriverButton);

            // Edit Vehicle button
            var editVehicleButton = ControlExtensions.CreateButton(
                "Edit Vehicle",
                new Point(270, 10),
                new Size(120, 30),
                (s, e) => {
                    if (driversListView.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Please select a vehicle to edit.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int vehicleId = int.Parse(driversListView.SelectedItems[0].Text);
                    ShowVehicleEditForm(vehicleId);
                }
            );
            driverTab.Controls.Add(editVehicleButton);

            // Delete Vehicle button
            var deleteVehicleButton = ControlExtensions.CreateButton(
                "Delete Vehicle",
                new Point(400, 10),
                new Size(120, 30),
                async (s, e) => {
                    if (driversListView.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Please select a vehicle to delete.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int vehicleId = int.Parse(driversListView.SelectedItems[0].Text);
                    string driverName = driversListView.SelectedItems[0].SubItems[1].Text;

                    if (MessageBox.Show($"Are you sure you want to delete vehicle {vehicleId} assigned to {driverName}?",
                            "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            // Delete vehicle logic
                            using (var cmd = new SQLiteCommand(dbService.GetConnection()))
                            {
                                cmd.CommandText = "DELETE FROM Vehicles WHERE VehicleID = @VehicleID";
                                cmd.Parameters.AddWithValue("@VehicleID", vehicleId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            await LoadVehiclesAsync();
                            await DisplayDriversAsync(driversListView);
                            MessageBox.Show("Vehicle deleted successfully.",
                                "Vehicle Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error deleting vehicle: {ex.Message}",
                                "Deletion Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            );
            driverTab.Controls.Add(deleteVehicleButton);

            // Map for vehicle locations
            var gMapControl = new GMap.NET.WindowsForms.GMapControl
            {
                Location = new Point(10, 460),
                Size = new Size(1140, 240),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };
            driverTab.Controls.Add(gMapControl);
            mapService.InitializeGoogleMaps(gMapControl);

            // Display vehicles on map when tab is shown
            tabControl.SelectedIndexChanged += async (s, e) => {
                if (tabControl.SelectedTab == driverTab)
                {
                    await LoadVehiclesAsync();
                    await DisplayDriversAsync(driversListView);
                    DisplayVehiclesOnMap(gMapControl);
                }
            };
        }

        private void SetupPassengersTab()
        {
            // Passengers ListView
            var passengersListView = new ListView
            {
                Location = new Point(10, 50),
                Size = new Size(1140, 400),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            passengersListView.Columns.Add("ID", 50);
            passengersListView.Columns.Add("Name", 150);
            passengersListView.Columns.Add("Location", 300);
            passengersListView.Columns.Add("Available Tomorrow", 120);
            passengersListView.Columns.Add("User ID", 80);
            passengersTab.Controls.Add(passengersListView);

            // Refresh button
            var refreshButton = ControlExtensions.CreateButton(
                "Refresh Passengers",
                new Point(10, 10),
                new Size(120, 30),
                async (s, e) => {
                    await LoadPassengersAsync();
                    await DisplayPassengersAsync(passengersListView);
                }
            );
            passengersTab.Controls.Add(refreshButton);

            // Add Passenger button
            var addPassengerButton = ControlExtensions.CreateButton(
                "Add Passenger",
                new Point(140, 10),
                new Size(120, 30),
                (s, e) => ShowPassengerEditForm(0)
            );
            passengersTab.Controls.Add(addPassengerButton);

            // Edit Passenger button
            var editPassengerButton = ControlExtensions.CreateButton(
                "Edit Passenger",
                new Point(270, 10),
                new Size(120, 30),
                (s, e) => {
                    if (passengersListView.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Please select a passenger to edit.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int passengerId = int.Parse(passengersListView.SelectedItems[0].Text);
                    ShowPassengerEditForm(passengerId);
                }
            );
            passengersTab.Controls.Add(editPassengerButton);

            // Delete Passenger button
            var deletePassengerButton = ControlExtensions.CreateButton(
                "Delete Passenger",
                new Point(400, 10),
                new Size(120, 30),
                async (s, e) => {
                    if (passengersListView.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Please select a passenger to delete.",
                            "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    int passengerId = int.Parse(passengersListView.SelectedItems[0].Text);
                    string passengerName = passengersListView.SelectedItems[0].SubItems[1].Text;

                    if (MessageBox.Show($"Are you sure you want to delete passenger {passengerName}?",
                            "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            // Delete passenger logic
                            using (var cmd = new SQLiteCommand(dbService.GetConnection()))
                            {
                                cmd.CommandText = "DELETE FROM Passengers WHERE PassengerID = @PassengerID";
                                cmd.Parameters.AddWithValue("@PassengerID", passengerId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            await LoadPassengersAsync();
                            await DisplayPassengersAsync(passengersListView);
                            MessageBox.Show($"Passenger {passengerName} deleted successfully.",
                                "Passenger Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error deleting passenger: {ex.Message}",
                                "Deletion Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            );
            passengersTab.Controls.Add(deletePassengerButton);

            // Map for passenger locations
            var gMapControl = new GMap.NET.WindowsForms.GMapControl
            {
                Location = new Point(10, 460),
                Size = new Size(1140, 240),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };
            passengersTab.Controls.Add(gMapControl);
            mapService.InitializeGoogleMaps(gMapControl);

            // Display passengers on map when tab is shown
            tabControl.SelectedIndexChanged += async (s, e) => {
                if (tabControl.SelectedTab == passengersTab)
                {
                    await LoadPassengersAsync();
                    await DisplayPassengersAsync(passengersListView);
                    DisplayPassengersOnMap(gMapControl);
                }
            };
        }

        private void SetupRoutesTab()
        {
            // Routes panel with date selection
            var dateSelector = new DateTimePicker
            {
                Location = new Point(140, 10),
                Size = new Size(200, 25),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today
            };
            routesTab.Controls.Add(dateSelector);

            routesTab.Controls.Add(ControlExtensions.CreateLabel(
                "Select Date:", new Point(10, 10), new Size(120, 25),
                null, ContentAlignment.MiddleRight));

            var loadButton = ControlExtensions.CreateButton(
                "Load Routes",
                new Point(350, 10),
                new Size(120, 30),
                async (s, e) => {
                    await LoadRoutesForDateAsync(dateSelector.Value);
                }
            );
            routesTab.Controls.Add(loadButton);

            // Routes display panel
            var routesPanel = ControlExtensions.CreatePanel(
                new Point(10, 50),
                new Size(1140, 660),
                BorderStyle.FixedSingle
            );
            routesTab.Controls.Add(routesPanel);

            // Map for route visualization 
            var gMapControl = new GMap.NET.WindowsForms.GMapControl
            {
                Location = new Point(10, 10),
                Size = new Size(700, 640),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };
            routesPanel.Controls.Add(gMapControl);
            mapService.InitializeGoogleMaps(gMapControl);

            // Routes list
            var routesListView = new ListView
            {
                Location = new Point(720, 10),
                Size = new Size(410, 320),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            routesListView.Columns.Add("Vehicle", 80);
            routesListView.Columns.Add("Driver", 120);
            routesListView.Columns.Add("Passengers", 80);
            routesListView.Columns.Add("Distance", 80);
            routeDetailsTextBox = new RichTextBox
            {
                Location = new Point(720, 340),
                Size = new Size(410, 310),
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None
            };
            routesPanel.Controls.Add(routesListView);
            routesPanel.Controls.Add(routeDetailsTextBox);

            // Add the "Get Google Routes" button
            // Create the button first without the event handler
            var getGoogleRoutesButton = new Button
            {
                Text = "Get Google Routes",
                Location = new Point(520, 10),
                Size = new Size(150, 30)
            };
            routesTab.Controls.Add(getGoogleRoutesButton);

            // Now add the event handler after the button is fully declared
            getGoogleRoutesButton.Click += async (s, e) => {
                try
                {
                    if (currentSolution == null)
                    {
                        MessageBox.Show("Please load a route first!", "No Route Loaded",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    getGoogleRoutesButton.Enabled = false;
                    getGoogleRoutesButton.Text = "Getting Routes...";

                    // Create a new routing service with the destination
                    var destination = await dbService.GetDestinationAsync();
                    var tempRoutingService = new RoutingService(mapService, destination.Latitude, destination.Longitude,destinationTargetTime);


                    if (TimeSpan.TryParse(destinationTargetTime, out TimeSpan targetTime))
                    {
                        DateTime targetDateTime = DateTime.Today.AddDays(1).Add(targetTime);
                        await tempRoutingService.GetGoogleRoutesAsync(gMapControl, currentSolution, targetDateTime);
                    }
                    else
                    {
                        // Fallback if time parsing fails
                        await tempRoutingService.GetGoogleRoutesAsync(gMapControl, currentSolution, null);
                    }
                    // Update the UI
                    UpdateRouteDetailsDisplay();

                    MessageBox.Show("Routes updated with Google API data!", "Routes Updated",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error getting Google routes: {ex.Message}",
                        "API Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    getGoogleRoutesButton.Enabled = true;
                    getGoogleRoutesButton.Text = "Get Google Routes";
                }
            };

            // Select route to view details
            routesListView.SelectedIndexChanged += (s, e) => {
                if (routesListView.SelectedItems.Count > 0)
                {
                    int vehicleId = int.Parse(routesListView.SelectedItems[0].SubItems[0].Text);
                    DisplayRouteDetails(vehicleId);
                }
            };
        }
        private void SetupDestinationTab()
        {
            // Destination edit panel
            var panel = ControlExtensions.CreatePanel(
                new Point(10, 50),
                new Size(1140, 660),
                BorderStyle.FixedSingle
            );
            destinationTab.Controls.Add(panel);

            // Name and time
            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Destination Name:", new Point(20, 20), new Size(150, 25)));
            var nameTextBox = ControlExtensions.CreateTextBox(
                new Point(180, 20), new Size(300, 25));
            panel.Controls.Add(nameTextBox);

            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Target Arrival Time:", new Point(20, 60), new Size(150, 25)));
            var timeTextBox = ControlExtensions.CreateTextBox(
                new Point(180, 60), new Size(150, 25), "08:00:00");
            panel.Controls.Add(timeTextBox);

            // update save Target arrival time
            var updateTimeButton = ControlExtensions.CreateButton(
                "Update Arrival Time",
                new Point(340, 60),
                new Size(150, 25),
                async (s, e) => await UpdateTargetTimeAsync(timeTextBox.Text)
            );
            panel.Controls.Add(updateTimeButton);

            // Map for destination selection
            var gMapControl = new GMap.NET.WindowsForms.GMapControl
            {
                Location = new Point(20, 120),
                Size = new Size(800, 500),
                MinZoom = 2,
                MaxZoom = 18,
                Zoom = 12,
                DragButton = MouseButtons.Left
            };
            panel.Controls.Add(gMapControl);
            mapService.InitializeGoogleMaps(gMapControl);

            // Location input
            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Latitude:", new Point(850, 120), new Size(80, 25)));
            var latTextBox = ControlExtensions.CreateTextBox(
                new Point(940, 120), new Size(180, 25));
            panel.Controls.Add(latTextBox);

            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Longitude:", new Point(850, 155), new Size(80, 25)));
            var lngTextBox = ControlExtensions.CreateTextBox(
                new Point(940, 155), new Size(180, 25));
            panel.Controls.Add(lngTextBox);

            // Address search
            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Address:", new Point(850, 190), new Size(80, 25)));
            var addressTextBox = ControlExtensions.CreateTextBox(
                new Point(940, 190), new Size(180, 60), "", true);
            panel.Controls.Add(addressTextBox);

            var searchButton = ControlExtensions.CreateButton(
                "Search Address",
                new Point(940, 260),
                new Size(180, 30),
                async (s, e) => {
                    try
                    {
                        var result = await mapService.GeocodeAddressAsync(addressTextBox.Text);
                        if (result.HasValue)
                        {
                            latTextBox.Text = result.Value.Latitude.ToString();
                            lngTextBox.Text = result.Value.Longitude.ToString();
                            gMapControl.Position = new GMap.NET.PointLatLng(result.Value.Latitude, result.Value.Longitude);
                            gMapControl.Zoom = 15;

                            // Show marker
                            gMapControl.Overlays.Clear();
                            var overlay = new GMap.NET.WindowsForms.GMapOverlay("destinationMarker");
                            var marker = new GMap.NET.WindowsForms.Markers.GMarkerGoogle(
                                new GMap.NET.PointLatLng(result.Value.Latitude, result.Value.Longitude),
                                GMap.NET.WindowsForms.Markers.GMarkerGoogleType.red
                            );
                            overlay.Markers.Add(marker);
                            gMapControl.Overlays.Add(overlay);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error searching address: {ex.Message}",
                            "Search Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            panel.Controls.Add(searchButton);

            // Save button
            var saveButton = ControlExtensions.CreateButton(
                "Save Destination",
                new Point(940, 310),
                new Size(180, 30),
                async (s, e) => {
                    try
                    {
                        // Validate inputs
                        if (string.IsNullOrWhiteSpace(nameTextBox.Text))
                        {
                            MessageBox.Show("Please enter a destination name.",
                                "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (!double.TryParse(latTextBox.Text, out double lat) ||
                            !double.TryParse(lngTextBox.Text, out double lng))
                        {
                            MessageBox.Show("Please enter valid coordinates.",
                                "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // Save destination
                        bool success = await dbService.UpdateDestinationAsync(
                            nameTextBox.Text, lat, lng, timeTextBox.Text, addressTextBox.Text);

                        if (success)
                        {
                            MessageBox.Show("Destination updated successfully.",
                                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            // Update stored values
                            destinationLat = lat;
                            destinationLng = lng;
                            destinationName = nameTextBox.Text;
                            destinationAddress = addressTextBox.Text;
                            destinationTargetTime = timeTextBox.Text;

                            // Reinitialize routing service with new destination
                            routingService = new RoutingService(mapService, destinationLat, destinationLng, destinationTargetTime);
                        }
                        else
                        {
                            MessageBox.Show("Failed to update destination.",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving destination: {ex.Message}",
                            "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            );
            panel.Controls.Add(saveButton);

            // Load destination info when tab is selected
            tabControl.SelectedIndexChanged += async (s, e) => {
            if (tabControl.SelectedTab == destinationTab)
            {
                var dest = await dbService.GetDestinationAsync();
                nameTextBox.Text = dest.Name;
                timeTextBox.Text = dest.TargetTime;
                latTextBox.Text = dest.Latitude.ToString();
                lngTextBox.Text = dest.Longitude.ToString();
                addressTextBox.Text = dest.Address;

                // Show on map
                gMapControl.Position = new GMap.NET.PointLatLng(dest.Latitude, dest.Longitude);
                gMapControl.Zoom = 15;
                    gMapControl.Overlays.Clear();
                    var overlay = new GMap.NET.WindowsForms.GMapOverlay("destinationMarker");
                    var marker = new GMap.NET.WindowsForms.Markers.GMarkerGoogle(
                        new GMap.NET.PointLatLng(dest.Latitude, dest.Longitude),
                        GMap.NET.WindowsForms.Markers.GMarkerGoogleType.red
                    );
                    overlay.Markers.Add(marker);
                    gMapControl.Overlays.Add(overlay);
                }
            };
        }

        private void SetupSchedulingTab()
        {
            // Scheduling configuration panel
            var panel = ControlExtensions.CreatePanel(
                new Point(10, 50),
                new Size(1140, 660),
                BorderStyle.FixedSingle
            );
            schedulingTab.Controls.Add(panel);

            // Enable scheduling checkbox
            var enabledCheckBox = ControlExtensions.CreateCheckBox(
                "Enable Automatic Scheduling",
                new Point(20, 20),
                new Size(250, 25),
                false  // Default unchecked
            );
            panel.Controls.Add(enabledCheckBox);

            // Time selection
            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Run Scheduler At:", new Point(20, 60), new Size(150, 25)));

            var timeSelector = new DateTimePicker
            {
                Location = new Point(180, 60),
                Size = new Size(120, 25),
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Value = DateTime.Parse("00:00:00")
            };
            panel.Controls.Add(timeSelector);

            // Save button - create without lambda first
            Button saveButton = new Button
            {
                Text = "Save Settings",
                Location = new Point(20, 100),
                Size = new Size(120, 30)
            };
            panel.Controls.Add(saveButton);

            // Run now button - create without lambda first
            Button runNowButton = new Button
            {
                Text = "Run Scheduler Now",
                Location = new Point(150, 100),
                Size = new Size(150, 30)
            };
            panel.Controls.Add(runNowButton);

            // Add Google API setting
            var useGoogleApiCheckBox = ControlExtensions.CreateCheckBox(
                "Always Use Google Routes API",
                new Point(20, 140),
                new Size(270, 20),
                true
            );
            panel.Controls.Add(useGoogleApiCheckBox);

            // History listview
            var historyListView = new ListView
            {
                Location = new Point(20, 200),
                Size = new Size(1100, 440),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            historyListView.Columns.Add("Date", 150);
            historyListView.Columns.Add("Status", 100);
            historyListView.Columns.Add("Routes Generated", 150);
            historyListView.Columns.Add("Passengers Assigned", 150);
            historyListView.Columns.Add("Run Time", 200);
            panel.Controls.Add(historyListView);

            // Section label
            panel.Controls.Add(ControlExtensions.CreateLabel(
                "Scheduling History:", new Point(20, 170), new Size(150, 25),
                new Font("Arial", 10, FontStyle.Bold)));

            // Refresh history button - create without lambda first
            Button refreshHistoryButton = new Button
            {
                Text = "Refresh History",
                Location = new Point(20, 650),
                Size = new Size(150, 30)
            };
            panel.Controls.Add(refreshHistoryButton);

            // Now add event handlers after all controls have been created
            saveButton.Click += async (s, e) => {
                try
                {
                    saveButton.Enabled = false;
                    saveButton.Text = "Saving...";

                    await dbService.SaveSchedulingSettingsAsync(
                        enabledCheckBox.Checked,
                        timeSelector.Value
                    );

                    MessageBox.Show("Scheduling settings saved successfully.",
                        "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving settings: {ex.Message}",
                        "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    saveButton.Enabled = true;
                    saveButton.Text = "Save Settings";
                }
            };

            // Save Google API setting
            useGoogleApiCheckBox.CheckedChanged += async (s, e) => {
                try
                {
                    await dbService.SaveSettingAsync("UseGoogleRoutesAPI",
                        useGoogleApiCheckBox.Checked ? "1" : "0");

                    MessageBox.Show(useGoogleApiCheckBox.Checked ?
                        "The scheduler will now use Google Routes API for all routes" :
                        "The scheduler will use estimated routes (no API calls)",
                        "Setting Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving setting: {ex.Message}",
                        "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            runNowButton.Click += async (s, e) => {
                if (MessageBox.Show("Are you sure you want to run the scheduler now? This will calculate routes for tomorrow.",
                        "Confirm Run", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        runNowButton.Enabled = false;
                        runNowButton.Text = "Running...";

                        await RunSchedulerAsync();
                        await RefreshHistoryListView(historyListView);

                        MessageBox.Show("Scheduler completed successfully.",
                            "Scheduler Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error running scheduler: {ex.Message}",
                            "Scheduler Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally
                    {
                        runNowButton.Enabled = true;
                        runNowButton.Text = "Run Scheduler Now";
                    }
                }
            };

            refreshHistoryButton.Click += async (s, e) => {
                try
                {
                    refreshHistoryButton.Enabled = false;
                    refreshHistoryButton.Text = "Refreshing...";
                    await RefreshHistoryListView(historyListView);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error refreshing history: {ex.Message}",
                        "Refresh Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    refreshHistoryButton.Enabled = true;
                    refreshHistoryButton.Text = "Refresh History";
                }
            };

            // Tab selection handler 
            tabControl.SelectedIndexChanged += async (s, e) => {
                if (tabControl.SelectedTab == schedulingTab)
                {
                    try
                    {
                        var settings = await dbService.GetSchedulingSettingsAsync();
                        enabledCheckBox.Checked = settings.IsEnabled;
                        timeSelector.Value = settings.ScheduledTime;

                        // Load Google API setting
                        var useGoogleApi = await dbService.GetSettingAsync("UseGoogleRoutesAPI", "1");
                        useGoogleApiCheckBox.Checked = useGoogleApi == "1";

                        await RefreshHistoryListView(historyListView);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading scheduling settings: {ex.Message}",
                            "Loading Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };
        }

        // Separate method for refreshing the history ListView
        private async Task RefreshHistoryListView(ListView listView)
        {
            if (listView == null)
                return;

            listView.Items.Clear();

            try
            {
                var history = await dbService.GetSchedulingLogAsync();

                foreach (var entry in history)
                {
                    var item = new ListViewItem(entry.RunTime.ToString("yyyy-MM-dd"));
                    item.SubItems.Add(entry.Status);
                    item.SubItems.Add(entry.RoutesGenerated.ToString());
                    item.SubItems.Add(entry.PassengersAssigned.ToString());
                    item.SubItems.Add(entry.RunTime.ToString("HH:mm:ss"));

                    // Set item color based on status
                    if (entry.Status == "Success")
                        item.ForeColor = Color.Green;
                    else if (entry.Status == "Failed" || entry.Status == "Error")
                        item.ForeColor = Color.Red;
                    else if (entry.Status == "Skipped")
                        item.ForeColor = Color.Orange;

                    listView.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error retrieving scheduling history: {ex.Message}",
                    "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Implementation of RunSchedulerAsync that's called from the Run Now button
        private async Task RunSchedulerAsync()
        {
            try
            {
                // Get destination information
                var destination = await dbService.GetDestinationAsync();

                // Get available vehicles and passengers
                var vehicles = await dbService.GetAvailableVehiclesAsync();
                var passengers = await dbService.GetAvailablePassengersAsync();

                // Only run if there are passengers and vehicles
                if (passengers.Count > 0 && vehicles.Count > 0)
                {
                    // Create a routing service
                    var routingService = new RoutingService(mapService, destination.Latitude, destination.Longitude , destinationTargetTime);

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
                     

                        // Save the solution to database for tomorrow's date
                        string tomorrowDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
                        int routeId = await dbService.SaveSolutionAsync(solution, tomorrowDate);

                        // Count assigned passengers and used vehicles
                        int assignedPassengers = solution.Vehicles.Sum(v => v.AssignedPassengers?.Count ?? 0);
                        int usedVehicles = solution.Vehicles.Count(v => v.AssignedPassengers?.Count > 0);

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
                        await dbService.LogSchedulingRunAsync(
                            DateTime.Now,
                            "Failed",
                            0,
                            0,
                            "Algorithm failed to find a valid solution"
                        );
                        throw new Exception("Algorithm failed to find a valid solution");
                    }
                }
                else
                {
                    await dbService.LogSchedulingRunAsync(
                        DateTime.Now,
                        "Skipped",
                        0,
                        0,
                        $"Insufficient participants: {passengers.Count} passengers, {vehicles.Count} vehicles"
                    );
                    throw new Exception($"No routes generated: {passengers.Count} passengers, {vehicles.Count} vehicles available");
                }
            }
            catch (Exception ex)
            {
                // Log exception
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

        // Method to calculate pickup times based on desired arrival time at destination
        private async Task CalculatePickupTimesBasedOnTargetArrival(Solution solution, string targetTimeString, RoutingService routingService)
        {
            // Parse target arrival time
            if (!TimeSpan.TryParse(targetTimeString, out TimeSpan targetTime))
            {
                targetTime = new TimeSpan(8, 0, 0); // Default to 8:00 AM
            }

            // Get the target time as DateTime for today (we'll use just the time portion)
            DateTime targetDateTime = DateTime.Today.Add(targetTime);

            foreach (var vehicle in solution.Vehicles)
            {
                if (vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
                    continue;

                RouteDetails routeDetails = null;
                if (routingService.VehicleRouteDetails.ContainsKey(vehicle.Id))
                {
                    routeDetails = routingService.VehicleRouteDetails[vehicle.Id];
                }
                if (routeDetails == null)
                    continue;

                // Get total trip time from start to destination in minutes
                double totalTripTime = routeDetails.TotalTime;

                // Calculate when driver needs to start to arrive at destination at target time
                DateTime driverStartTime = targetDateTime.AddMinutes(-totalTripTime);

                // Store the driver's departure time
                vehicle.DepartureTime = driverStartTime.ToString("HH:mm");

                // Now calculate each passenger's pickup time based on cumulative time from start
                double cumulativeTimeFromStart = 0;
                for (int i = 0; i < vehicle.AssignedPassengers.Count; i++)
                {
                    var passenger = vehicle.AssignedPassengers[i];

                    // Find corresponding stop detail
                    var stopDetail = routeDetails.StopDetails.FirstOrDefault(s => s.PassengerId == passenger.Id);
                    if (stopDetail != null)
                    {
                        cumulativeTimeFromStart = stopDetail.CumulativeTime;

                        // Calculate pickup time based on driver start time plus cumulative time to this passenger
                        DateTime pickupTime = driverStartTime.AddMinutes(cumulativeTimeFromStart);
                        passenger.EstimatedPickupTime = pickupTime.ToString("HH:mm");
                    }
                }
            }
        }

        // Helper method to convert target time to minutes
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

        #endregion

        #region Data Loading and Display

        private async Task LoadAllDataAsync()
        {
            await LoadUsersAsync();
            await LoadVehiclesAsync();
            await LoadPassengersAsync();
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                // Get all users from the database
                users = await dbService.GetAllUsersAsync();

                // Log the success
                Console.WriteLine($"Loaded {users.Count} users from database");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading users: {ex.Message}",
                    "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // If database loading fails, fall back to sample data for testing purposes
                users = new List<(int Id, string Username, string UserType, string Name, string Email, string Phone)>
                {
                    (1, "admin", "Admin", "Administrator", "", ""),
                    (2, "driver1", "Driver", "John Driver", "", ""),
                    (3, "passenger1", "Passenger", "Alice Passenger", "", "")
                };
            }
        }

        private async Task LoadVehiclesAsync()
        {
            try
            {
                // In a real implementation, this would be:
                vehicles = await dbService.GetAllVehiclesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading vehicles: {ex.Message}",
                    "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Fallback to empty list
                vehicles = new List<Vehicle>();
            }
        }

        private async Task LoadPassengersAsync()
        {
            try
            {
                // In a real implementation, this would be:
                passengers = await dbService.GetAllPassengersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading passengers: {ex.Message}",
                    "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Fallback to empty list
                passengers = new List<Passenger>();
            }
        }

        private async Task DisplayUsersAsync(ListView listView)
        {
            listView.Items.Clear();

            if (users == null)
                return;

            foreach (var user in users)
            {
                var item = new ListViewItem(user.Id.ToString());
                item.SubItems.Add(user.Username);
                item.SubItems.Add(user.UserType);
                item.SubItems.Add(user.Name);
                item.SubItems.Add(user.Email);
                item.SubItems.Add(user.Phone);

                listView.Items.Add(item);
            }
        }

        private async Task DisplayDriversAsync(ListView listView)
        {
            listView.Items.Clear();

            if (vehicles == null)
                return;

            foreach (var vehicle in vehicles)
            {
                var item = new ListViewItem(vehicle.Id.ToString());
                item.SubItems.Add(vehicle.DriverName ?? $"User {vehicle.UserId}");
                item.SubItems.Add(vehicle.Capacity.ToString());

                string location = !string.IsNullOrEmpty(vehicle.StartAddress)
                    ? vehicle.StartAddress
                    : $"({vehicle.StartLatitude:F4}, {vehicle.StartLongitude:F4})";
                item.SubItems.Add(location);

                item.SubItems.Add(vehicle.IsAvailableTomorrow ? "Yes" : "No");
                item.SubItems.Add(vehicle.UserId.ToString());

                listView.Items.Add(item);
            }
        }

        private async Task DisplayPassengersAsync(ListView listView)
        {
            listView.Items.Clear();

            if (passengers == null)
                return;

            foreach (var passenger in passengers)
            {
                var item = new ListViewItem(passenger.Id.ToString());
                item.SubItems.Add(passenger.Name);

                string location = !string.IsNullOrEmpty(passenger.Address)
                    ? passenger.Address
                    : $"({passenger.Latitude:F4}, {passenger.Longitude:F4})";
                item.SubItems.Add(location);

                item.SubItems.Add(passenger.IsAvailableTomorrow ? "Yes" : "No");
                item.SubItems.Add(passenger.UserId.ToString());

                listView.Items.Add(item);
            }
        }

        private void DisplayVehiclesOnMap(GMap.NET.WindowsForms.GMapControl mapControl)
        {
            if (vehicles == null || mapControl == null) return;

            mapControl.Overlays.Clear();
            var overlay = new GMap.NET.WindowsForms.GMapOverlay("vehicles");

            foreach (var vehicle in vehicles)
            {
                var marker = MapOverlays.CreateVehicleMarker(vehicle);
                overlay.Markers.Add(marker);
            }

            // Add destination marker
            var destMarker = MapOverlays.CreateDestinationMarker(destinationLat, destinationLng);
            overlay.Markers.Add(destMarker);

            mapControl.Overlays.Add(overlay);

            // Center map on first vehicle or destination
            if (vehicles.Any())
            {
                var firstVehicle = vehicles.First();
                mapControl.Position = new GMap.NET.PointLatLng(
                    firstVehicle.StartLatitude, firstVehicle.StartLongitude);
            }
            else
            {
                mapControl.Position = new GMap.NET.PointLatLng(destinationLat, destinationLng);
            }

            mapControl.Zoom = 12;
        }

        private void DisplayPassengersOnMap(GMap.NET.WindowsForms.GMapControl mapControl)
        {
            if (passengers == null || mapControl == null) return;

            mapControl.Overlays.Clear();
            var overlay = new GMap.NET.WindowsForms.GMapOverlay("passengers");

            foreach (var passenger in passengers)
            {
                var marker = MapOverlays.CreatePassengerMarker(passenger);
                overlay.Markers.Add(marker);
            }

            // Add destination marker
            var destMarker = MapOverlays.CreateDestinationMarker(destinationLat, destinationLng);
            overlay.Markers.Add(destMarker);

            mapControl.Overlays.Add(overlay);

            // Center map on first passenger or destination
            if (passengers.Any())
            {
                var firstPassenger = passengers.First();
                mapControl.Position = new GMap.NET.PointLatLng(
                    firstPassenger.Latitude, firstPassenger.Longitude);
            }
            else
            {
                mapControl.Position = new GMap.NET.PointLatLng(destinationLat, destinationLng);
            }

            mapControl.Zoom = 12;
        }

        private async Task LoadRoutesForDateAsync(DateTime date)
        {
            try
            {
                string dateString = date.ToString("yyyy-MM-dd");
                currentSolution = await dbService.GetSolutionForDateAsync(dateString);

                if (currentSolution == null || currentSolution.Vehicles.Count == 0)
                {
                    MessageBox.Show($"No routes found for {date.ToShortDateString()}.",
                        "No Routes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Display routes on map
                var routesPanel = routesTab.Controls.OfType<Panel>().FirstOrDefault();
                var gMapControl = routesPanel?.Controls.OfType<GMap.NET.WindowsForms.GMapControl>().FirstOrDefault();
                var routesListView = routesPanel?.Controls.OfType<ListView>().FirstOrDefault();

                if (gMapControl != null)
                {
                    routingService.DisplaySolutionOnMap(gMapControl, currentSolution);
                    var passengersOverlay = new GMapOverlay("passengers");
                    foreach (var vehicle in currentSolution.Vehicles)
                    {
                        foreach (var passenger in vehicle.AssignedPassengers)
                        {
                            var marker = MapOverlays.CreatePassengerMarker(passenger);
                            passengersOverlay.Markers.Add(marker);
                        }
                    }
                    gMapControl.Overlays.Add(passengersOverlay);
                }

                // Display routes in list
                if (routesListView != null)
                {
                    routesListView.Items.Clear();

                    foreach (var vehicle in currentSolution.Vehicles.Where(v => v.AssignedPassengers.Count > 0))
                    {
                        var item = new ListViewItem(vehicle.Id.ToString());
                        item.SubItems.Add(vehicle.DriverName ?? $"Driver {vehicle.Id}");
                        item.SubItems.Add(vehicle.AssignedPassengers.Count.ToString());
                        item.SubItems.Add($"{vehicle.TotalDistance:F2} km");
                        routesListView.Items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading routes: {ex.Message}",
                    "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisplayRouteDetails(int vehicleId)
        {
            if (currentSolution == null) return;

            var vehicle = currentSolution.Vehicles.FirstOrDefault(v => v.Id == vehicleId);
            if (vehicle == null) return;

            routeDetailsTextBox.Clear();
            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText($"Route Details for Vehicle {vehicleId}\n\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;

            routeDetailsTextBox.AppendText($"Driver: {vehicle.DriverName ?? $"Driver {vehicleId}"}\n");
            routeDetailsTextBox.AppendText($"Vehicle Capacity: {vehicle.Capacity}\n");

            // Ensure distance values match list view
            if (routingService != null && routingService.VehicleRouteDetails.ContainsKey(vehicle.Id))
            {
                var routeDetails = routingService.VehicleRouteDetails[vehicle.Id];
                routeDetailsTextBox.AppendText($"Total Distance: {routeDetails.TotalDistance:F2} km\n");
                routeDetailsTextBox.AppendText($"Total Time: {routeDetails.TotalTime:F2} minutes\n");

            }
            //else
            //{
            //    routeDetailsTextBox.AppendText($"Total Distance: {vehicle.TotalDistance:F2} km\n");
            //    routeDetailsTextBox.AppendText($"Total Time: {vehicle.TotalTime:F2} minutes\n");
            //}

            // Format and display departure time
            if (!string.IsNullOrEmpty(vehicle.DepartureTime))
            {
                string formattedDepartureTime = claudpro.Utilities.TimeFormatUtility.FormatTimeDisplay(vehicle.DepartureTime);
                routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                routeDetailsTextBox.AppendText($"Departure Time: {formattedDepartureTime}\n");
                routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
            }
            routeDetailsTextBox.AppendText($"Total Distance: {vehicle.TotalDistance:F2} km\n");
            routeDetailsTextBox.AppendText($"Total Time: {vehicle.TotalTime:F2} minutes\n");

 

            routeDetailsTextBox.AppendText("\n");
            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
            routeDetailsTextBox.AppendText("Pickup Order:\n");
            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;

            if (vehicle.AssignedPassengers.Count == 0)
            {
                routeDetailsTextBox.AppendText("No passengers assigned to this vehicle.\n");
                return;
            }

            for (int i = 0; i < vehicle.AssignedPassengers.Count; i++)
            {
                var passenger = vehicle.AssignedPassengers[i];
                routeDetailsTextBox.AppendText($"{i + 1}. {passenger.Name}\n");

                string location = !string.IsNullOrEmpty(passenger.Address)
                    ? passenger.Address
                    : $"({passenger.Latitude:F4}, {passenger.Longitude:F4})";
                routeDetailsTextBox.AppendText($"   Pickup at: {location}\n");

                // Display estimated pickup time using centralized formatter
                if (!string.IsNullOrEmpty(passenger.EstimatedPickupTime))
                {
                    string formattedPickupTime = claudpro.Utilities.TimeFormatUtility.FormatTimeDisplay(passenger.EstimatedPickupTime);
                    routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                    routeDetailsTextBox.AppendText($"   Estimated pickup time: {formattedPickupTime}\n");
                    routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
                }

                routeDetailsTextBox.AppendText("\n");
            }
        }
        private void UpdateRouteDetailsDisplay()
        {
            routeDetailsTextBox.Clear();

            if (routingService.VehicleRouteDetails.Count == 0)
            {
                routeDetailsTextBox.AppendText("No route details available.\n\n");
                routeDetailsTextBox.AppendText("Load a route and use the 'Get Google Routes' button to see detailed timing information.");
                return;
            }

            foreach (var detail in routingService.VehicleRouteDetails.Values.OrderBy(d => d.VehicleId))
            {
                // Get vehicle info
                var vehicle = currentSolution.Vehicles.FirstOrDefault(v => v.Id == detail.VehicleId);
                string startLocation = vehicle != null && !string.IsNullOrEmpty(vehicle.StartAddress)
                    ? vehicle.StartAddress
                    : $"({vehicle?.StartLatitude ?? 0:F4}, {vehicle?.StartLongitude ?? 0:F4})";

                routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                routeDetailsTextBox.AppendText($"Vehicle {detail.VehicleId}\n");
                routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
                routeDetailsTextBox.AppendText($"Start Location: {startLocation}\n");
                routeDetailsTextBox.AppendText($"Driver: {vehicle?.DriverName ?? "Unknown"}\n");
                routeDetailsTextBox.AppendText($"Total Distance: {detail.TotalDistance:F2} km\n");
                routeDetailsTextBox.AppendText($"Total Time: {detail.TotalTime:F2} min\n");

                // Display departure time using the centralized formatter
                if (!string.IsNullOrEmpty(vehicle?.DepartureTime))
                {
                    string formattedDepartureTime = claudpro.Utilities.TimeFormatUtility.FormatTimeDisplay(vehicle.DepartureTime);
                    routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                    routeDetailsTextBox.AppendText($"Departure Time: {formattedDepartureTime}\n");
                    routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
                }

                routeDetailsTextBox.AppendText("\n");

                routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                routeDetailsTextBox.AppendText("Stop Details:\n");
                routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;

                int stopNumber = 1;
                foreach (var stop in detail.StopDetails)
                {
                    // For stops that are passengers
                    if (stop.PassengerId >= 0)
                    {
                        var passenger = vehicle?.AssignedPassengers?.FirstOrDefault(p => p.Id == stop.PassengerId);
                        string stopName = passenger != null ? passenger.Name : $"Passenger {stop.PassengerName}";
                        string stopLocation = passenger != null && !string.IsNullOrEmpty(passenger.Address)
                            ? passenger.Address
                            : $"({passenger?.Latitude ?? 0:F4}, {passenger?.Longitude ?? 0:F4})";

                        routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                        routeDetailsTextBox.AppendText($"{stopNumber}. {stopName}\n");
                        routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
                        routeDetailsTextBox.AppendText($"   Location: {stopLocation}\n");

                        // Display estimated pickup time using the centralized formatter
                        if (passenger != null && !string.IsNullOrEmpty(passenger.EstimatedPickupTime))
                        {
                            string formattedPickupTime =claudpro.Utilities.TimeFormatUtility.FormatTimeDisplay(passenger.EstimatedPickupTime);
                            routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                            routeDetailsTextBox.AppendText($"   Pickup Time: {formattedPickupTime}\n");
                            routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
                        }
                    }
                    else // For destination stop
                    {
                        string stopName = "Destination";

                        // Try to get destination address
                        string stopLocation = string.Empty;
                        try
                        {
                            var dest = dbService.GetDestinationAsync().GetAwaiter().GetResult();
                            stopLocation = !string.IsNullOrEmpty(dest.Address)
                                ? dest.Address
                                : $"({dest.Latitude:F4}, {dest.Longitude:F4})";
                        }
                        catch
                        {
                            stopLocation = $"({destinationLat:F4}, {destinationLng:F4})";
                        }

                        routeDetailsTextBox.SelectionFont = new Font(routeDetailsTextBox.Font, FontStyle.Bold);
                        routeDetailsTextBox.AppendText($"{stopNumber}. {stopName}\n");
                        routeDetailsTextBox.SelectionFont = routeDetailsTextBox.Font;
                        routeDetailsTextBox.AppendText($"   Location: {stopLocation}\n");
                    }

                    routeDetailsTextBox.AppendText($"   Distance: {stop.DistanceFromPrevious:F2} km\n");
                    routeDetailsTextBox.AppendText($"   Time: {stop.TimeFromPrevious:F2} min\n");
                    routeDetailsTextBox.AppendText($"   Cumulative: {stop.CumulativeDistance:F2} km, {stop.CumulativeTime:F2} min\n\n");
                    stopNumber++;
                }

                routeDetailsTextBox.AppendText("--------------------------------\n\n");
            }
        }
        #endregion

        #region Helper Methods

        async Task UpdateTargetTimeAsync(string timeString)
        {
            try
            {
                // Validate time format (HH:MM:SS)
                if (!TimeSpan.TryParse(timeString, out TimeSpan _))
                {
                    MessageBox.Show("Please enter a valid time in format HH:MM:SS",
                        "Invalid Time Format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Get current destination values
                var dest = await dbService.GetDestinationAsync();

                // Update just the target time
                bool success = await dbService.UpdateDestinationAsync(
                    dest.Name, dest.Latitude, dest.Longitude, timeString, dest.Address);

                if (success)
                {
                    MessageBox.Show("Target arrival time updated successfully.",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Update stored value
                    destinationTargetTime = timeString;
                }
                else
                {
                    MessageBox.Show("Failed to update target arrival time.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating target time: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void ShowVehicleEditForm(int vehicleId)
        {
            // This would be implemented to show a form for editing vehicle details
            MessageBox.Show("Vehicle edit functionality would be implemented here.",
                "Not Implemented", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowPassengerEditForm(int passengerId)
        {
            // This would be implemented to show a form for editing passenger details
            MessageBox.Show("Passenger edit functionality would be implemented here.",
                "Not Implemented", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AdminForm_Load(object sender, EventArgs e)
        {
            // Load initial data
            Task.Run(async () =>
            {
                await LoadAllDataAsync();

                // Update UI on main thread
                if (this.IsHandleCreated)
                {
                    this.Invoke(new Action(async () =>
                    {
                        await DisplayUsersAsync(usersListView);
                    }));
                }
            });
        }

        #endregion
    }
}