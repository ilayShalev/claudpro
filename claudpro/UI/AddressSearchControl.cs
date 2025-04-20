using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using claudpro.Services;
using GMap.NET.WindowsForms;

namespace claudpro.UI
{
    public class AddressSearchControl : UserControl
    {
        private readonly MapService mapService;
        private TextBox addressTextBox;
        private Button searchButton;
        private GMapControl mapControl;
        private Label statusLabel;

        public event EventHandler<AddressFoundEventArgs> AddressFound;

        public string Address
        {
            get => addressTextBox?.Text ?? string.Empty;
            set
            {
                if (addressTextBox != null)
                    addressTextBox.Text = value;
            }
        }

        public AddressSearchControl(MapService mapService, GMapControl mapControl)
        {
            this.mapService = mapService ?? throw new ArgumentNullException(nameof(mapService));
            this.mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Create address label
            var addressLabel = new Label
            {
                Text = "Address:",
                Location = new Point(0, 5),
                Size = new Size(70, 20)
            };
            Controls.Add(addressLabel);

            // Create address text box
            addressTextBox = new TextBox
            {
                Location = new Point(70, 3),
                Size = new Size(200, 20)
            };
            addressTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    SearchAddressAsync().ConfigureAwait(false);
                }
            };
            Controls.Add(addressTextBox);

            // Create search button
            searchButton = new Button
            {
                Text = "Search",
                Location = new Point(280, 1),
                Size = new Size(70, 24)
            };
            searchButton.Click += async (s, e) => await SearchAddressAsync();
            Controls.Add(searchButton);

            // Create status label
            statusLabel = new Label
            {
                Text = "",
                Location = new Point(0, 30),
                Size = new Size(350, 20),
                ForeColor = Color.Red,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };
            Controls.Add(statusLabel);

            // Set control properties
            Size = new Size(350, 50);
        }

        public async Task SearchAddressAsync()
        {
            if (string.IsNullOrWhiteSpace(addressTextBox.Text))
            {
                ShowStatus("Please enter an address", false);
                return;
            }

            try
            {
                // Show searching status
                searchButton.Enabled = false;
                addressTextBox.Enabled = false;
                ShowStatus("Searching...", false);

                // Geocode the address
                var result = await mapService.GeocodeAddressAsync(addressTextBox.Text);
                if (result.HasValue)
                {
                    double latitude = result.Value.Latitude;
                    double longitude = result.Value.Longitude;

                    // Get the formatted address
                    string formattedAddress = await mapService.ReverseGeocodeAsync(latitude, longitude)
                        ?? addressTextBox.Text;

                    // Update the text box with the formatted address
                    addressTextBox.Text = formattedAddress;

                    // Center map on the found location
                    mapControl.Position = new PointLatLng(latitude, longitude);
                    mapControl.Zoom = 15;

                    // Add a temporary marker to show the found location
                    var overlay = new GMap.NET.WindowsForms.GMapOverlay("searchResult");
                    var marker = new GMap.NET.WindowsForms.Markers.GMarkerGoogle(
                        new PointLatLng(latitude, longitude),
                        GMap.NET.WindowsForms.Markers.GMarkerGoogleType.yellow);
                    overlay.Markers.Add(marker);
                    mapControl.Overlays.Add(overlay);

                    // Remove the marker after 5 seconds
                    await System.Threading.Tasks.Task.Delay(5000).ContinueWith(t =>
                    {
                        if (IsDisposed) return;

                        this.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                mapControl.Overlays.Remove(overlay);
                                mapControl.Refresh();
                            }
                            catch { /* Ignore errors during cleanup */ }
                        }));
                    });

                    // Notify listeners
                    OnAddressFound(new AddressFoundEventArgs(
                        latitude, longitude, formattedAddress));

                    // Show success status
                    ShowStatus("Address found", true);
                }
                else
                {
                    ShowStatus("Address not found", false);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}", false);
            }
            finally
            {
                // Re-enable controls
                searchButton.Enabled = true;
                addressTextBox.Enabled = true;
            }
        }

        private void ShowStatus(string message, bool success)
        {
            statusLabel.Text = message;
            statusLabel.ForeColor = success ? Color.Green : Color.Red;
            statusLabel.Visible = true;

            // Hide status message after 5 seconds
            System.Threading.Tasks.Task.Delay(5000).ContinueWith(t =>
            {
                if (IsDisposed) return;

                this.BeginInvoke(new Action(() =>
                {
                    statusLabel.Visible = false;
                }));
            });
        }

        protected virtual void OnAddressFound(AddressFoundEventArgs e)
        {
            AddressFound?.Invoke(this, e);
        }
    }

    public class AddressFoundEventArgs : EventArgs
    {
        public double Latitude { get; }
        public double Longitude { get; }
        public string FormattedAddress { get; }

        public AddressFoundEventArgs(double latitude, double longitude, string formattedAddress)
        {
            Latitude = latitude;
            Longitude = longitude;
            FormattedAddress = formattedAddress;
        }
    }
}