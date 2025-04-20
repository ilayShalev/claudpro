using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Windows.Forms;
using System.Configuration;
using System.Drawing;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using Newtonsoft.Json;
using claudpro.Models;
using claudpro.Utilities;

namespace claudpro.Services
{
    public class MapService : IDisposable
    {
        private readonly string apiKey;
        private readonly HttpClient httpClient;
        private readonly Dictionary<string, List<PointLatLng>> routeCache = new Dictionary<string, List<PointLatLng>>();
        private bool isDisposed = false;

        public MapService(string apiKey = null)
        {
            // Use provided API key or try to load from config
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = ConfigurationManager.AppSettings["GoogleApiKey"];

                // If still empty, show a dialog to enter it
                if (string.IsNullOrEmpty(apiKey))
                {
                    using (var form = new Form())
                    {
                        form.Width = 400;
                        form.Height = 150;
                        form.Text = "Google API Key Required";

                        var label = new Label { Left = 20, Top = 20, Text = "Please enter your Google Maps API Key:", Width = 360 };
                        var textBox = new TextBox { Left = 20, Top = 50, Width = 360 };
                        var button = new Button { Text = "OK", Left = 160, Top = 80, DialogResult = DialogResult.OK };

                        form.Controls.Add(label);
                        form.Controls.Add(textBox);
                        form.Controls.Add(button);
                        form.AcceptButton = button;

                        if (form.ShowDialog() == DialogResult.OK)
                        {
                            apiKey = textBox.Text;

                            // Optionally save to config for future use
                            try
                            {
                                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                                config.AppSettings.Settings["GoogleApiKey"].Value = apiKey;
                                config.Save(ConfigurationSaveMode.Modified);
                                ConfigurationManager.RefreshSection("appSettings");
                            }
                            catch { /* Ignore save errors */ }
                        }
                    }
                }
            }

            this.apiKey = apiKey;
            this.httpClient = new HttpClient();

            // Set timeout for HTTP requests
            this.httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Initialize GMap providers
            try
            {
                // Initialize GMap
                GMaps.Instance.Mode = AccessMode.ServerAndCache;

                // Set API key for providers
                GoogleMapProvider.Instance.ApiKey = apiKey;
                GoogleSatelliteMapProvider.Instance.ApiKey = apiKey;
                GoogleHybridMapProvider.Instance.ApiKey = apiKey;
                GoogleTerrainMapProvider.Instance.ApiKey = apiKey;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing map providers: {ex.Message}",
                    "Map Initialization Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Initializes the Google Maps component with error handling
        /// </summary>
        public bool InitializeGoogleMaps(GMapControl mapControl, double latitude = 32.0853, double longitude = 34.7818)
        {
            if (mapControl == null) return false;

            try
            {
                // Set map control properties
                mapControl.MapProvider = GoogleMapProvider.Instance;
                mapControl.Position = new PointLatLng(latitude, longitude);
                mapControl.MinZoom = 2;
                mapControl.MaxZoom = 18;
                mapControl.Zoom = 12;
                mapControl.DragButton = MouseButtons.Left;
                mapControl.CanDragMap = true;
                mapControl.ShowCenter = false;

                // Check if provider is working
                // Initialize GMap provider
                try
                {
                    GMaps.Instance.Mode = AccessMode.ServerAndCache;
                    GMaps.Instance.UseRouteCache = true;
                    GMaps.Instance.UsePlacemarkCache = true;
                }
                catch (Exception ex)
                {
                    // Just log the error but continue - the map might still work
                    Console.WriteLine($"Warning initializing GMaps: {ex.Message}");
                }

                // Enable map events
                mapControl.OnMapZoomChanged += () =>
                {
                    // Save zoom level or perform other actions when zoom changes
                };

                mapControl.Refresh();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing map: {ex.Message}",
                    "Map Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Changes the map provider type with error handling
        /// </summary>
        public bool ChangeMapProvider(GMapControl mapControl, int providerType)
        {
            if (mapControl == null) return false;

            try
            {
                switch (providerType)
                {
                    case 0: mapControl.MapProvider = GoogleMapProvider.Instance; break;
                    case 1: mapControl.MapProvider = GoogleSatelliteMapProvider.Instance; break;
                    case 2: mapControl.MapProvider = GoogleHybridMapProvider.Instance; break;
                    case 3: mapControl.MapProvider = GoogleTerrainMapProvider.Instance; break;
                    default: mapControl.MapProvider = GoogleMapProvider.Instance; break;
                }

                mapControl.Refresh();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error changing map provider: {ex.Message}",
                    "Map Provider Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
        }

        /// <summary>
        /// Fetches directions from Google Maps Directions API
        /// </summary>
        public async Task<List<PointLatLng>> GetGoogleDirectionsAsync(List<PointLatLng> waypoints, DateTime? targetArrivalTime = null)
        {
            if (waypoints == null || waypoints.Count < 2) return null;

            try
            {
                string cacheKey = string.Join("|", waypoints.Select(p => $"{p.Lat},{p.Lng}"));
                if (targetArrivalTime.HasValue)
                {
                    cacheKey += $"|arrival_{targetArrivalTime.Value.ToString("yyyy-MM-dd HH:mm:ss")}";
                }

                if (routeCache.ContainsKey(cacheKey)) return routeCache[cacheKey];

                var origin = waypoints[0];
                var destination = waypoints.Last();
                var intermediates = waypoints.Skip(1).Take(waypoints.Count - 2).ToList();

                string url = $"https://maps.googleapis.com/maps/api/directions/json?" +
                    $"origin={origin.Lat},{origin.Lng}&" +
                    $"destination={destination.Lat},{destination.Lng}&" +
                    (intermediates.Any() ? $"waypoints={string.Join("|", intermediates.Select(p => $"{p.Lat},{p.Lng}"))}&" : "");

                // הוספת פרמטר זמן הגעה אם סופק
                if (targetArrivalTime.HasValue)
                {
                    // וידוא שהתאריך הוא עתידי - אם התאריך קטן מהעכשיו, נשתמש במחר
                    DateTime futureArrivalTime = targetArrivalTime.Value;
                    if (futureArrivalTime <= DateTime.Now)
                    {
                        // הערה: אנחנו משתמשים בתאריך של מחר עם השעה המקורית
                        // שעות בפורמט 24 שעות נשמרות נכון
                        futureArrivalTime = DateTime.Today.AddDays(1) + targetArrivalTime.Value.TimeOfDay;

                        // בדיקה אם אחרי התיקון עדיין לא בעתיד
                        if (futureArrivalTime <= DateTime.Now)
                        {
                            // אם עדיין בעבר, נוסיף עוד יום
                            futureArrivalTime = futureArrivalTime.AddDays(1);
                        }
                    }

                    // המרת התאריך לפורמט Unix timestamp
                    var unixTimestamp = (long)((DateTimeOffset)futureArrivalTime).ToUnixTimeSeconds();
                    url += $"arrival_time={unixTimestamp}&";

                    Console.WriteLine($"Using future arrival time: {futureArrivalTime.ToString("yyyy-MM-dd HH:mm:ss")} (Unix: {unixTimestamp})");
                }

                url += $"key={apiKey}";

                var response = await httpClient.GetStringAsync(url);
                dynamic data = JsonConvert.DeserializeObject(response);

                if (data.status != "OK")
                {
                    string errorMessage = data.status.ToString();
                    if (data.error_message != null)
                    {
                        errorMessage += $": {data.error_message.ToString()}";
                    }
                    throw new Exception($"Google Directions API error: {errorMessage}");
                }

                var points = new List<PointLatLng>();
                foreach (var leg in data.routes[0].legs)
                    foreach (var step in leg.steps)
                        points.AddRange(PolylineEncoder.Decode(step.polyline.points.ToString()));

                routeCache[cacheKey] = points;
                return points;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error getting directions: {ex.Message}",
                    "Directions Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return null;
            }
        }
        /// Gets route details from the Google Directions API
        /// </summary>
        // Update the GetRouteDetailsAsync method in MapService.cs to properly handle time formats

        public async Task<RouteDetails> GetRouteDetailsAsync(Vehicle vehicle, double destinationLat, double destinationLng, DateTime? targetArrivalTime = null)
        {
            if (vehicle == null || vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
                return null;

            try
            {
                // Build waypoints
                string origin = $"{vehicle.StartLatitude},{vehicle.StartLongitude}";
                string destination = $"{destinationLat},{destinationLng}";
                string waypointsStr = string.Join("|", vehicle.AssignedPassengers.Select(p => $"{p.Latitude},{p.Longitude}"));

                string url = $"https://maps.googleapis.com/maps/api/directions/json?" +
                    $"origin={origin}" +
                    $"&destination={destination}" +
                    (vehicle.AssignedPassengers.Any() ? $"&waypoints={waypointsStr}" : "");

                // If a target arrival time is provided, use it
                if (targetArrivalTime.HasValue)
                {
                    // Always ensure the target time is in the future
                    DateTime futureArrivalTime = targetArrivalTime.Value;

                    if (futureArrivalTime <= DateTime.Now)
                    {
                        // If today's time has already passed, use tomorrow with the same time of day
                        futureArrivalTime = DateTime.Today.AddDays(1).Add(targetArrivalTime.Value.TimeOfDay);

                        // Double-check it's still in the future
                        if (futureArrivalTime <= DateTime.Now)
                        {
                            futureArrivalTime = futureArrivalTime.AddDays(1);
                        }
                    }

                    // Convert to Unix timestamp for Google API
                    var unixTimestamp = (long)((DateTimeOffset)futureArrivalTime).ToUnixTimeSeconds();
                    url += $"&arrival_time={unixTimestamp}";

                    Console.WriteLine($"Using future arrival time: {futureArrivalTime.ToString("yyyy-MM-dd HH:mm:ss")} (Unix: {unixTimestamp})");
                }

                url += $"&key={apiKey}";

                var response = await httpClient.GetStringAsync(url);
                dynamic data = JsonConvert.DeserializeObject(response);

                if (data.status.ToString() != "OK")
                {
                    string errorMessage = data.status.ToString();
                    if (data.error_message != null)
                    {
                        errorMessage += $": {data.error_message.ToString()}";
                    }
                    throw new Exception($"Google Directions API error: {errorMessage}");
                }

                var routeDetail = new RouteDetails
                {
                    VehicleId = vehicle.Id,
                    StopDetails = new List<StopDetail>()
                };

                double totalDistance = 0;
                double totalTime = 0;

                // Process legs (segments between consecutive points)
                for (int i = 0; i < data.routes[0].legs.Count; i++)
                {
                    var leg = data.routes[0].legs[i];

                    // Extract information from response
                    double distance = Convert.ToDouble(leg.distance.value) / 1000.0; // Convert meters to km
                    double time = Convert.ToDouble(leg.duration.value) / 60.0; // Convert seconds to minutes

                    totalDistance += distance;
                    totalTime += time;

                    string stopName = i < vehicle.AssignedPassengers.Count
                        ? vehicle.AssignedPassengers[i].Name
                        : "Destination";

                    int passengerId = i < vehicle.AssignedPassengers.Count
                        ? vehicle.AssignedPassengers[i].Id
                        : -1;

                    // Process arrival time from API response - ensure consistent 24-hour format
                    string estimatedArrivalTime = null;
                    if (leg.arrival_time != null)
                    {
                        // First try to parse the time directly from text field
                        if (!string.IsNullOrEmpty(leg.arrival_time.text.ToString()))
                        {
                            string apiTimeText = leg.arrival_time.text.ToString();
                            if (DateTime.TryParse(apiTimeText, out DateTime parsedTime))
                            {
                                // Store in 24-hour format
                                estimatedArrivalTime = parsedTime.ToString("HH:mm");
                            }
                        }

                        // If text parsing failed, try using the value field which is sometimes a timestamp
                        if (string.IsNullOrEmpty(estimatedArrivalTime) && leg.arrival_time.value != null)
                        {
                            try
                            {
                                long timestamp = Convert.ToInt64(leg.arrival_time.value.ToString());
                                var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime.ToLocalTime();
                                estimatedArrivalTime = dateTime.ToString("HH:mm");
                            }
                            catch { /* Ignore conversion errors */ }
                        }

                        Console.WriteLine($"Stop {i + 1} arrival time from API: {estimatedArrivalTime}");
                    }

                    // Process departure time similarly
                    string estimatedDepartureTime = null;
                    if (leg.departure_time != null)
                    {
                        // First try to parse the time directly from text field
                        if (!string.IsNullOrEmpty(leg.departure_time.text.ToString()))
                        {
                            string apiTimeText = leg.departure_time.text.ToString();
                            if (DateTime.TryParse(apiTimeText, out DateTime parsedTime))
                            {
                                // Store in 24-hour format
                                estimatedDepartureTime = parsedTime.ToString("HH:mm");
                            }
                        }

                        // If text parsing failed, try using the value field
                        if (string.IsNullOrEmpty(estimatedDepartureTime) && leg.departure_time.value != null)
                        {
                            try
                            {
                                long timestamp = Convert.ToInt64(leg.departure_time.value.ToString());
                                var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime.ToLocalTime();
                                estimatedDepartureTime = dateTime.ToString("HH:mm");
                            }
                            catch { /* Ignore conversion errors */ }
                        }

                        Console.WriteLine($"Stop {i + 1} departure time from API: {estimatedDepartureTime}");
                    }

                    routeDetail.StopDetails.Add(new StopDetail
                    {
                        StopNumber = i + 1,
                        PassengerId = passengerId,
                        PassengerName = stopName,
                        DistanceFromPrevious = distance,
                        TimeFromPrevious = time,
                        CumulativeDistance = totalDistance,
                        CumulativeTime = totalTime,
                        EstimatedArrivalTime = estimatedArrivalTime,
                        EstimatedDepartureTime = estimatedDepartureTime
                    });
                }

                // Save initial departure time from API response
                if (data.routes[0].legs.Count > 0 && data.routes[0].legs[0].departure_time != null)
                {
                    string departureTimeText = null;

                    // First try to parse from text field
                    if (!string.IsNullOrEmpty(data.routes[0].legs[0].departure_time.text.ToString()))
                    {
                        string apiTimeText = data.routes[0].legs[0].departure_time.text.ToString();
                        if (DateTime.TryParse(apiTimeText, out DateTime parsedTime))
                        {
                            // Store in 24-hour format
                            departureTimeText = parsedTime.ToString("HH:mm");
                        }
                    }

                    // If text parsing failed, try using the value field
                    if (string.IsNullOrEmpty(departureTimeText) && data.routes[0].legs[0].departure_time.value != null)
                    {
                        try
                        {
                            long timestamp = Convert.ToInt64(data.routes[0].legs[0].departure_time.value.ToString());
                            var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime.ToLocalTime();
                            departureTimeText = dateTime.ToString("HH:mm");
                        }
                        catch { /* Ignore conversion errors */ }
                    }

                    if (!string.IsNullOrEmpty(departureTimeText))
                    {
                        vehicle.DepartureTime = departureTimeText;
                        routeDetail.DepartureTime = departureTimeText;
                        Console.WriteLine($"Vehicle departure time from API (24-hour format): {departureTimeText}");
                    }
                }

                routeDetail.TotalDistance = totalDistance;
                routeDetail.TotalTime = totalTime;

                return routeDetail;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting route details: {ex.Message}");
                throw;
            }
        }
        /// </summary>
        /// 


        public RouteDetails EstimateRouteDetails(Vehicle vehicle, double destinationLat, double destinationLng)
        {
            if (vehicle == null || vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
                return null;

            try
            {
                var routeDetail = new RouteDetails
                {
                    VehicleId = vehicle.Id,
                    TotalDistance = 0,
                    TotalTime = 0,
                    StopDetails = new List<StopDetail>()
                };

                // Calculate time from vehicle start to first passenger
                double currentLat = vehicle.StartLatitude;
                double currentLng = vehicle.StartLongitude;
                double totalDistance = 0;
                double totalTime = 0;

                for (int i = 0; i < vehicle.AssignedPassengers.Count; i++)
                {
                    var passenger = vehicle.AssignedPassengers[i];
                    if (passenger == null) continue;

                    double distance = GeoCalculator.CalculateDistance(currentLat, currentLng, passenger.Latitude, passenger.Longitude);
                    double time = (distance / 30.0) * 60; // Assuming 30 km/h average speed

                    totalDistance += distance;
                    totalTime += time;

                    routeDetail.StopDetails.Add(new StopDetail
                    {
                        StopNumber = i + 1,
                        PassengerId = passenger.Id,
                        PassengerName = passenger.Name,
                        DistanceFromPrevious = distance,
                        TimeFromPrevious = time,
                        CumulativeDistance = totalDistance,
                        CumulativeTime = totalTime
                    });

                    currentLat = passenger.Latitude;
                    currentLng = passenger.Longitude;
                }

                // Calculate trip to final destination
                double distToDest = GeoCalculator.CalculateDistance(currentLat, currentLng, destinationLat, destinationLng);
                double timeToDest = (distToDest / 30.0) * 60;

                totalDistance += distToDest;
                totalTime += timeToDest;

                routeDetail.StopDetails.Add(new StopDetail
                {
                    StopNumber = vehicle.AssignedPassengers.Count + 1,
                    PassengerId = -1,
                    PassengerName = "Destination",
                    DistanceFromPrevious = distToDest,
                    TimeFromPrevious = timeToDest,
                    CumulativeDistance = totalDistance,
                    CumulativeTime = totalTime
                });

                routeDetail.TotalDistance = totalDistance;
                routeDetail.TotalTime = totalTime;

                return routeDetail;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error estimating route details: {ex.Message}",
                   "Route Estimation Error",
                   MessageBoxButtons.OK,
                   MessageBoxIcon.Warning);
                return null;
            }
        }

        /// <summary>
        /// Gets a color for a route based on the route index
        /// </summary>
        public Color GetRouteColor(int index)
        {
            Color[] routeColors = {
                Color.FromArgb(255, 128, 0),   // Orange
                Color.FromArgb(128, 0, 128),   // Purple
                Color.FromArgb(0, 128, 128),   // Teal
                Color.FromArgb(128, 0, 0),     // Maroon
                Color.FromArgb(0, 128, 0),     // Green
                Color.FromArgb(0, 0, 128),     // Navy
                Color.FromArgb(128, 128, 0),   // Olive
                Color.FromArgb(128, 0, 64)     // Burgundy
            };
            return routeColors[index % routeColors.Length];
        }

        /// <summary>
        /// Geocodes an address string to latitude and longitude coordinates
        /// </summary>
        /// <param name="address">The address to geocode</param>
        /// <returns>A tuple with latitude and longitude if successful, null if failed</returns>
        public async Task<(double Latitude, double Longitude)?> GeocodeAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;

            try
            {
                // URL encode the address
                string encodedAddress = Uri.EscapeDataString(address);
                string url = $"https://maps.googleapis.com/maps/api/geocode/json?address={encodedAddress}&key={apiKey}";

                var response = await httpClient.GetStringAsync(url);
                dynamic data = JsonConvert.DeserializeObject(response);

                if (data.status.ToString() != "OK" || data.results.Count == 0)
                {
                    string errorMessage = data.status.ToString();
                    if (data.error_message != null)
                    {
                        errorMessage += $": {data.error_message.ToString()}";
                    }
                    throw new Exception($"Geocoding error: {errorMessage}");
                }

                double lat = Convert.ToDouble(data.results[0].geometry.location.lat);
                double lng = Convert.ToDouble(data.results[0].geometry.location.lng);

                return (lat, lng);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error geocoding address: {ex.Message}",
                   "Geocoding Error",
                   MessageBoxButtons.OK,
                   MessageBoxIcon.Warning);
                return null;
            }
        }

        /// <summary>
        /// Gets a formatted address from coordinates (reverse geocoding)
        /// </summary>
        /// <param name="latitude">The latitude</param>
        /// <param name="longitude">The longitude</param>
        /// <returns>A formatted address string if successful, null if failed</returns>
        public async Task<string> ReverseGeocodeAsync(double latitude, double longitude)
        {
            try
            {
                string url = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={latitude},{longitude}&key={apiKey}";

                var response = await httpClient.GetStringAsync(url);
                dynamic data = JsonConvert.DeserializeObject(response);

                if (data.status.ToString() != "OK" || data.results.Count == 0)
                {
                    string errorMessage = data.status.ToString();
                    if (data.error_message != null)
                    {
                        errorMessage += $": {data.error_message.ToString()}";
                    }
                    throw new Exception($"Reverse geocoding error: {errorMessage}");
                }

                return data.results[0].formatted_address.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reverse geocoding: {ex.Message}",
                   "Reverse Geocoding Error",
                   MessageBoxButtons.OK,
                   MessageBoxIcon.Warning);
                return null;
            }
        }

        /// <summary>
        /// Searches for address suggestions based on a partial address
        /// </summary>
        /// <param name="query">The partial address to search for</param>
        /// <returns>A list of address suggestions</returns>
        public async Task<List<string>> GetAddressSuggestionsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<string>();

            try
            {
                string encodedQuery = Uri.EscapeDataString(query);
                string url = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?input={encodedQuery}&key={apiKey}";

                var response = await httpClient.GetStringAsync(url);
                dynamic data = JsonConvert.DeserializeObject(response);

                if (data.status.ToString() != "OK")
                {
                    string errorMessage = data.status.ToString();
                    if (data.error_message != null)
                    {
                        errorMessage += $": {data.error_message.ToString()}";
                    }
                    throw new Exception($"Autocomplete error: {errorMessage}");
                }

                var suggestions = new List<string>();
                foreach (var prediction in data.predictions)
                {
                    suggestions.Add(prediction.description.ToString());
                }

                return suggestions;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting address suggestions: {ex.Message}");
                return new List<string>();
            }
        }

        // IDisposable implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    httpClient?.Dispose();
                }

                isDisposed = true;
            }
        }

        ~MapService()
        {
            Dispose(false);
        }
    }
}