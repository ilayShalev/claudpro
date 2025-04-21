using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using Newtonsoft.Json;
using claudpro.Models;
using claudpro.Utilities;
using System.Linq;

namespace claudpro.Services
{
    public class MapService : IDisposable
    {
        private readonly string apiKey;
        private readonly System.Net.Http.HttpClient httpClient;
        private readonly Dictionary<string, List<PointLatLng>> routeCache = new Dictionary<string, List<PointLatLng>>();
        private bool isDisposed = false;

        // API request limiter
        private DateTime lastApiCall = DateTime.MinValue;
        private const int MinApiCallIntervalMs = 300; // Minimum time between API calls
        private const int MaxRetries = 3; // Maximum retry attempts for API calls

        // Cache expiration timespan
        private const int CacheExpirationHours = 24;

        public MapService()
        {
            this.httpClient = new System.Net.Http.HttpClient();

            // Set timeout for HTTP requests
            this.httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Initialize GMap providers
            try
            {
                // Initialize GMap
                GMaps.Instance.Mode = AccessMode.ServerAndCache;

                InitializeApiKey();
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Error,
                    "Error initializing map providers", true);
            }
        }

        public MapService(string apiKey)
        {
            // Initialize the httpClient
            this.httpClient = new System.Net.Http.HttpClient();

            // Set the API key
            this.apiKey = apiKey;

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
                Console.WriteLine($"Error initializing map providers: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes API key for map providers
        /// </summary>
        private async void InitializeApiKey()
        {
            try
            {
                string apiKey = await ApiKeyManager.GetGoogleApiKeyAsync();

                // Set API key for providers
                GoogleMapProvider.Instance.ApiKey = apiKey;
                GoogleSatelliteMapProvider.Instance.ApiKey = apiKey;
                GoogleHybridMapProvider.Instance.ApiKey = apiKey;
                GoogleTerrainMapProvider.Instance.ApiKey = apiKey;
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Warning,
                    "Failed to initialize API key for map providers", true);
            }
        }

        /// <summary>
        /// Initializes the Google Maps component with error handling
        /// </summary>
        public bool InitializeGoogleMaps(GMapControl mapControl, double latitude = 32.0853, double longitude = 34.7818, int zoom = 12)
        {
            if (mapControl == null) return false;

            try
            {
                // Set map control properties
                mapControl.MapProvider = GoogleMapProvider.Instance;
                mapControl.Position = new PointLatLng(latitude, longitude);
                mapControl.MinZoom = 2;
                mapControl.MaxZoom = 18;
                mapControl.Zoom = zoom;
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
                    ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Warning,
                        "Warning initializing GMaps", false);
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
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Error,
                    "Error initializing map", true);
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
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Warning,
                    "Error changing map provider", true);
                return false;
            }
        }

        /// <summary>
        /// Fetches directions from Google Maps Directions API with retries and rate limiting
        /// </summary>
        public async Task<List<PointLatLng>> GetGoogleDirectionsAsync(List<PointLatLng> waypoints, DateTime? targetArrivalTime = null)
        {
            if (waypoints == null || waypoints.Count < 2) return null;

            // Generate cache key
            string cacheKey = string.Join("|", waypoints.Select(p => $"{p.Lat},{p.Lng}"));
            if (targetArrivalTime.HasValue)
            {
                cacheKey += $"|arrival_{targetArrivalTime.Value.ToString("yyyy-MM-dd HH:mm:ss")}";
            }

            // Check cache first
            if (routeCache.ContainsKey(cacheKey))
                return routeCache[cacheKey];

            try
            {
                // Get API key
                string apiKey = await ApiKeyManager.GetGoogleApiKeyAsync();
                if (string.IsNullOrEmpty(apiKey))
                {
                    ErrorHandler.LogMessage("No Google API key available",
                        ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Warning);
                    return null;
                }

                // Apply rate limiting
                await RateLimitApiRequestAsync();

                var origin = waypoints[0];
                var destination = waypoints.Last();
                var intermediates = waypoints.Skip(1).Take(waypoints.Count - 2).ToList();

                string url = $"https://maps.googleapis.com/maps/api/directions/json?" +
                    $"origin={origin.Lat},{origin.Lng}&" +
                    $"destination={destination.Lat},{destination.Lng}&" +
                    (intermediates.Any() ? $"waypoints={string.Join("|", intermediates.Select(p => $"{p.Lat},{p.Lng}"))}&" : "");

                // Add target arrival time parameter if provided
                if (targetArrivalTime.HasValue)
                {
                    // Ensure the target time is in the future
                    DateTime futureArrivalTime = TimeFormatUtility.EnsureFutureDateTime(targetArrivalTime.Value);

                    // Convert to Unix timestamp
                    long unixTimestamp = TimeFormatUtility.ToUnixTimestamp(futureArrivalTime);
                    url += $"arrival_time={unixTimestamp}&";

                    ErrorHandler.LogMessage(
                        $"Using future arrival time in directions request: {futureArrivalTime:yyyy-MM-dd HH:mm:ss} (Unix: {unixTimestamp})",
                        ErrorHandler.ErrorCategory.Mapping,
                        ErrorHandler.ErrorSeverity.Information);
                }

                // Add API key
                url += $"key={apiKey}";

                // Execute request with retries
                string response = await ExecuteWithRetriesAsync(async () => await httpClient.GetStringAsync(url));
                dynamic data = JsonConvert.DeserializeObject(response);

                if (data.status.ToString() != "OK")
                {
                    string errorMessage = data.status.ToString();
                    if (data.error_message != null)
                    {
                        errorMessage += $": {data.error_message.ToString()}";
                    }

                    ErrorHandler.LogMessage($"Google Directions API error: {errorMessage}",
                        ErrorHandler.ErrorCategory.Network, ErrorHandler.ErrorSeverity.Warning);

                    return null;
                }

                // Decode polyline points
                var points = new List<PointLatLng>();
                foreach (var leg in data.routes[0].legs)
                {
                    foreach (var step in leg.steps)
                    {
                        points.AddRange(PolylineEncoder.Decode(step.polyline.points.ToString()));
                    }
                }

                // Cache the result
                routeCache[cacheKey] = points;

                return points;
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Network, ErrorHandler.ErrorSeverity.Warning,
                    "Error getting directions from Google API", false);
                return null;
            }
        }

        /// <summary>
        /// Gets route details from the Google Directions API with improved error handling
        /// </summary>
        public async Task<RouteDetails> GetRouteDetailsAsync(Vehicle vehicle, double destinationLat, double destinationLng, DateTime? targetArrivalTime = null)
        {
            if (vehicle == null || vehicle.AssignedPassengers == null || vehicle.AssignedPassengers.Count == 0)
                return null;

            try
            {
                // Get API key
                string apiKey = await ApiKeyManager.GetGoogleApiKeyAsync();
                if (string.IsNullOrEmpty(apiKey))
                {
                    ErrorHandler.LogMessage("No Google API key available",
                        ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Warning);
                    return EstimateRouteDetails(vehicle, destinationLat, destinationLng);
                }

                // Apply rate limiting
                await RateLimitApiRequestAsync();

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
                    DateTime futureArrivalTime = TimeFormatUtility.EnsureFutureDateTime(targetArrivalTime.Value);

                    // Convert to Unix timestamp for Google API
                    long unixTimestamp = TimeFormatUtility.ToUnixTimestamp(futureArrivalTime);
                    url += $"&arrival_time={unixTimestamp}";

                    ErrorHandler.LogMessage(
                        $"Using future arrival time in route details request: {futureArrivalTime:yyyy-MM-dd HH:mm:ss} (Unix: {unixTimestamp})",
                        ErrorHandler.ErrorCategory.Mapping,
                        ErrorHandler.ErrorSeverity.Information);
                }

                // Add API key
                url += $"&key={apiKey}";

                // Execute request with retries
                string response = await ExecuteWithRetriesAsync(async () => await httpClient.GetStringAsync(url));
                dynamic data = JsonConvert.DeserializeObject(response);

                if (data.status.ToString() != "OK")
                {
                    string errorMessage = data.status.ToString();
                    if (data.error_message != null)
                    {
                        errorMessage += $": {data.error_message.ToString()}";
                    }

                    ErrorHandler.LogMessage($"Google Directions API error: {errorMessage}",
                        ErrorHandler.ErrorCategory.Network, ErrorHandler.ErrorSeverity.Warning);

                    // Fall back to estimated route details
                    return EstimateRouteDetails(vehicle, destinationLat, destinationLng);
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

                    // Process arrival time from API response with consistent formatting
                    string estimatedArrivalTime = null;
                    if (leg.arrival_time != null)
                    {
                        // Try to parse the time from text field first
                        if (!string.IsNullOrEmpty(leg.arrival_time.text.ToString()))
                        {
                            string apiTimeText = leg.arrival_time.text.ToString();
                            if (TimeFormatUtility.ParseToDateTime(apiTimeText, out DateTime parsedTime))
                            {
                                // Store in standard format
                                estimatedArrivalTime = TimeFormatUtility.FormatTimeStorage(parsedTime);
                            }
                        }

                        // If text parsing failed, try using the value field which is a timestamp
                        if (string.IsNullOrEmpty(estimatedArrivalTime) && leg.arrival_time.value != null)
                        {
                            try
                            {
                                long timestamp = Convert.ToInt64(leg.arrival_time.value.ToString());
                                var dateTime = TimeFormatUtility.FromUnixTimestamp(timestamp);
                                estimatedArrivalTime = TimeFormatUtility.FormatTimeStorage(dateTime);
                            }
                            catch { /* Ignore conversion errors */ }
                        }
                    }

                    // Process departure time similarly with consistent formatting
                    string estimatedDepartureTime = null;
                    if (leg.departure_time != null)
                    {
                        // First try to parse from text field
                        if (!string.IsNullOrEmpty(leg.departure_time.text.ToString()))
                        {
                            string apiTimeText = leg.departure_time.text.ToString();
                            if (TimeFormatUtility.ParseToDateTime(apiTimeText, out DateTime parsedTime))
                            {
                                // Store in standard format
                                estimatedDepartureTime = TimeFormatUtility.FormatTimeStorage(parsedTime);
                            }
                        }

                        // If text parsing failed, try using the value field
                        if (string.IsNullOrEmpty(estimatedDepartureTime) && leg.departure_time.value != null)
                        {
                            try
                            {
                                long timestamp = Convert.ToInt64(leg.departure_time.value.ToString());
                                var dateTime = TimeFormatUtility.FromUnixTimestamp(timestamp);
                                estimatedDepartureTime = TimeFormatUtility.FormatTimeStorage(dateTime);
                            }
                            catch { /* Ignore conversion errors */ }
                        }
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
                        if (TimeFormatUtility.ParseToDateTime(apiTimeText, out DateTime parsedTime))
                        {
                            // Store in standard format
                            departureTimeText = TimeFormatUtility.FormatTimeStorage(parsedTime);
                        }
                    }

                    // If text parsing failed, try using the value field
                    if (string.IsNullOrEmpty(departureTimeText) && data.routes[0].legs[0].departure_time.value != null)
                    {
                        try
                        {
                            long timestamp = Convert.ToInt64(data.routes[0].legs[0].departure_time.value.ToString());
                            var dateTime = TimeFormatUtility.FromUnixTimestamp(timestamp);
                            departureTimeText = TimeFormatUtility.FormatTimeStorage(dateTime);
                        }
                        catch { /* Ignore conversion errors */ }
                    }

                    if (!string.IsNullOrEmpty(departureTimeText))
                    {
                        vehicle.DepartureTime = departureTimeText;
                        routeDetail.DepartureTime = departureTimeText;

                        ErrorHandler.LogMessage(
                            $"Vehicle departure time from API (standardized format): {departureTimeText}",
                            ErrorHandler.ErrorCategory.Routing,
                            ErrorHandler.ErrorSeverity.Information);
                    }
                }

                routeDetail.TotalDistance = totalDistance;
                routeDetail.TotalTime = totalTime;

                return routeDetail;
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Network, ErrorHandler.ErrorSeverity.Warning,
                    "Error getting route details from Google API, using estimated route instead", false);

                // Fall back to estimated route details
                return EstimateRouteDetails(vehicle, destinationLat, destinationLng);
            }
        }

        /// <summary>
        /// Estimates route details without using Google API
        /// </summary>
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
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Routing, ErrorHandler.ErrorSeverity.Error,
                    "Error estimating route details", false);
                return null;
            }
        }

        /// <summary>
        /// Gets a color for a route based on the route index
        /// </summary>
        public System.Drawing.Color GetRouteColor(int index)
        {
            System.Drawing.Color[] routeColors = {
                System.Drawing.Color.FromArgb(255, 128, 0),   // Orange
                System.Drawing.Color.FromArgb(128, 0, 128),   // Purple
                System.Drawing.Color.FromArgb(0, 128, 128),   // Teal
                System.Drawing.Color.FromArgb(128, 0, 0),     // Maroon
                System.Drawing.Color.FromArgb(0, 128, 0),     // Green
                System.Drawing.Color.FromArgb(0, 0, 128),     // Navy
                System.Drawing.Color.FromArgb(128, 128, 0),   // Olive
                System.Drawing.Color.FromArgb(128, 0, 64)     // Burgundy
            };
            return routeColors[index % routeColors.Length];
        }

        /// <summary>
        /// Geocodes an address string to latitude and longitude coordinates
        /// </summary>
        public async Task<(double Latitude, double Longitude, string FormattedAddress)?> GeocodeAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;

            try
            {
                // Get API key
                string apiKey = await ApiKeyManager.GetGoogleApiKeyAsync();
                if (string.IsNullOrEmpty(apiKey))
                {
                    ErrorHandler.LogMessage("No Google API key available",
                        ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Warning);
                    return null;
                }

                // Apply rate limiting
                await RateLimitApiRequestAsync();

                // URL encode the address
                string encodedAddress = Uri.EscapeDataString(address);
                string url = $"https://maps.googleapis.com/maps/api/geocode/json?address={encodedAddress}&key={apiKey}";

                // Execute request with retries
                string response = await ExecuteWithRetriesAsync(async () => await httpClient.GetStringAsync(url));
                dynamic data = JsonConvert.DeserializeObject(response);

                if (data.status.ToString() != "OK" || data.results.Count == 0)
                {
                    string errorMessage = data.status.ToString();
                    if (data.error_message != null)
                    {
                        errorMessage += $": {data.error_message.ToString()}";
                    }

                    ErrorHandler.LogMessage($"Geocoding error: {errorMessage}",
                        ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Warning);

                    return null;
                }

                double lat = Convert.ToDouble(data.results[0].geometry.location.lat);
                double lng = Convert.ToDouble(data.results[0].geometry.location.lng);
                string formattedAddress = data.results[0].formatted_address.ToString();

                return (lat, lng, formattedAddress);
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Network, ErrorHandler.ErrorSeverity.Warning,
                    "Error geocoding address", true);
                return null;
            }
        }

        /// <summary>
        /// Gets a formatted address from coordinates (reverse geocoding)
        /// </summary>
        public async Task<string> ReverseGeocodeAsync(double latitude, double longitude)
        {
            try
            {
                // Get API key// Get API key
                string apiKey = await ApiKeyManager.GetGoogleApiKeyAsync();
                if (string.IsNullOrEmpty(apiKey))
                {
                    ErrorHandler.LogMessage("No Google API key available",
                        ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Warning);
                    return null;
                }

                // Apply rate limiting
                await RateLimitApiRequestAsync();

                string url = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={latitude},{longitude}&key={apiKey}";

                // Execute request with retries
                string response = await ExecuteWithRetriesAsync(async () => await httpClient.GetStringAsync(url));
                dynamic data = JsonConvert.DeserializeObject(response);

                if (data.status.ToString() != "OK" || data.results.Count == 0)
                {
                    string errorMessage = data.status.ToString();
                    if (data.error_message != null)
                    {
                        errorMessage += $": {data.error_message.ToString()}";
                    }

                    ErrorHandler.LogMessage($"Reverse geocoding error: {errorMessage}",
                        ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Warning);

                    // Fallback to basic coordinate format
                    return $"Location ({latitude:F6}, {longitude:F6})";
                }

                return data.results[0].formatted_address.ToString();
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Network, ErrorHandler.ErrorSeverity.Warning,
                    "Error reverse geocoding", false);

                // Fallback to basic coordinate format
                return $"Location ({latitude:F6}, {longitude:F6})";
            }
        }

        /// <summary>
        /// Searches for address suggestions based on a partial address
        /// </summary>
        public async Task<List<string>> GetAddressSuggestionsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<string>();

            try
            {
                // Get API key
                string apiKey = await ApiKeyManager.GetGoogleApiKeyAsync();
                if (string.IsNullOrEmpty(apiKey))
                {
                    ErrorHandler.LogMessage("No Google API key available",
                        ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Warning);
                    return new List<string>();
                }

                // Apply rate limiting
                await RateLimitApiRequestAsync();

                string encodedQuery = Uri.EscapeDataString(query);
                string url = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?input={encodedQuery}&key={apiKey}";

                // Execute request with retries
                string response = await ExecuteWithRetriesAsync(async () => await httpClient.GetStringAsync(url));
                dynamic data = JsonConvert.DeserializeObject(response);

                if (data.status.ToString() != "OK")
                {
                    string errorMessage = data.status.ToString();
                    if (data.error_message != null)
                    {
                        errorMessage += $": {data.error_message.ToString()}";
                    }

                    ErrorHandler.LogMessage($"Autocomplete error: {errorMessage}",
                        ErrorHandler.ErrorCategory.Mapping, ErrorHandler.ErrorSeverity.Warning);

                    return new List<string>();
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
                ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Network, ErrorHandler.ErrorSeverity.Warning,
                    "Error getting address suggestions", false);
                return new List<string>();
            }
        }

        /// <summary>
        /// Applies rate limiting to API requests
        /// </summary>
        private async Task RateLimitApiRequestAsync()
        {
            // Calculate time since last API call
            TimeSpan sinceLastCall = DateTime.Now - lastApiCall;

            // If we made a call too recently, wait to avoid rate limits
            if (sinceLastCall.TotalMilliseconds < MinApiCallIntervalMs)
            {
                int delayMs = MinApiCallIntervalMs - (int)sinceLastCall.TotalMilliseconds;
                await Task.Delay(delayMs);
            }

            // Update the timestamp
            lastApiCall = DateTime.Now;
        }

        /// <summary>
        /// Executes an API request with retries
        /// </summary>
        private async Task<string> ExecuteWithRetriesAsync(Func<Task<string>> requestFunc)
        {
            int retryCount = 0;
            TimeSpan retryDelay = TimeSpan.FromSeconds(1);

            while (true)
            {
                try
                {
                    return await requestFunc();
                }
                catch (Exception ex)
                {
                    retryCount++;

                    if (retryCount >= MaxRetries)
                    {
                        // Log and rethrow if max retries reached
                        ErrorHandler.LogError(ex, ErrorHandler.ErrorCategory.Network, ErrorHandler.ErrorSeverity.Warning,
                            $"API request failed after {MaxRetries} retries", false);
                        throw;
                    }

                    // Log retry attempt
                    ErrorHandler.LogMessage($"API request failed, retrying ({retryCount}/{MaxRetries}) after {retryDelay.TotalSeconds}s: {ex.Message}",
                        ErrorHandler.ErrorCategory.Network, ErrorHandler.ErrorSeverity.Information);

                    // Wait before retrying
                    await Task.Delay(retryDelay);

                    // Exponential backoff
                    retryDelay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * 2);
                }
            }
        }

        /// <summary>
        /// Clears the route cache
        /// </summary>
        public void ClearCache()
        {
            routeCache.Clear();
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