using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;
using System.Collections.Generic;

namespace claudpro.Services
{
    /// <summary>
    /// Manages API key storage and retrieval securely
    /// </summary>
    public class ApiKeyManager
    {
        private const string GoogleApiKeyName = "GoogleApiKey";
        private const string EncryptedKeyPrefix = "ENC:";
        private static readonly byte[] _entropyBytes = Encoding.UTF8.GetBytes("RideMatchSecureStorage");

        // File for storing encrypted keys
        private static readonly string _secureKeyFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RideMatch",
            "secure_keys.dat");

        // In-memory cache of API keys to reduce decryption overhead
        private static string _cachedGoogleApiKey;

        /// <summary>
        /// Initializes the API key manager
        /// </summary>
        static ApiKeyManager()
        {
            // Ensure the directory exists
            string directory = Path.GetDirectoryName(_secureKeyFile);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Gets the Google API key, prompting for it if not available
        /// </summary>
        public static async Task<string> GetGoogleApiKeyAsync(bool forcePrompt = false)
        {
            // Check memory cache first
            if (!forcePrompt && !string.IsNullOrEmpty(_cachedGoogleApiKey))
            {
                return _cachedGoogleApiKey;
            }

            // Try to get from config
            string apiKey = await GetApiKeyAsync(GoogleApiKeyName);

            // If still not found or force prompting, ask the user
            if (string.IsNullOrEmpty(apiKey) || forcePrompt)
            {
                apiKey = await PromptForApiKeyAsync("Please enter your Google Maps API Key:");

                if (!string.IsNullOrEmpty(apiKey))
                {
                    await SetApiKeyAsync(GoogleApiKeyName, apiKey);
                }
            }

            // Cache the result
            _cachedGoogleApiKey = apiKey;
            return apiKey;
        }

        /// <summary>
        /// Retrieves an API key from secure storage
        /// </summary>
        public static async Task<string> GetApiKeyAsync(string keyName)
        {
            // First try to get from config if not encrypted
            string configValue = ConfigurationManager.AppSettings[keyName];
            if (!string.IsNullOrEmpty(configValue) && !configValue.StartsWith(EncryptedKeyPrefix))
            {
                return configValue;
            }

            // Try to get from secure storage
            try
            {
                if (File.Exists(_secureKeyFile))
                {
                    // Read all keys from the secure file
                    string content = await Task.Run(() => File.ReadAllText(_secureKeyFile));
                    foreach (string line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length == 2 && parts[0] == keyName)
                        {
                            return DecryptValue(parts[1]);
                        }
                    }
                }

                // If keyname exists in config with ENC: prefix, try to decrypt it
                if (!string.IsNullOrEmpty(configValue) && configValue.StartsWith(EncryptedKeyPrefix))
                {
                    return DecryptValue(configValue.Substring(EncryptedKeyPrefix.Length));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving API key: {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// Stores an API key in secure storage
        /// </summary>
        public static async Task SetApiKeyAsync(string keyName, string keyValue)
        {
            if (string.IsNullOrEmpty(keyName) || string.IsNullOrEmpty(keyValue))
                return;

            try
            {
                // Encrypt the key
                string encryptedValue = EncryptValue(keyValue);

                // Read existing keys
                Dictionary<string, string> keys = new Dictionary<string, string>();
                if (File.Exists(_secureKeyFile))
                {
                    string content = await Task.Run(() => File.ReadAllText(_secureKeyFile));
                    foreach (string line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length == 2 && parts[0] != keyName) // Skip the key we're updating
                        {
                            keys[parts[0]] = parts[1];
                        }
                    }
                }

                // Add/update our key
                keys[keyName] = encryptedValue;

                // Write back all keys
                StringBuilder sb = new StringBuilder();
                foreach (var kvp in keys)
                {
                    sb.AppendLine($"{kvp.Key}={kvp.Value}");
                }

                await Task.Run(() => File.WriteAllText(_secureKeyFile, sb.ToString()));

                // Also try to update app.config for legacy support
                try
                {
                    var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    if (config.AppSettings.Settings[keyName] != null)
                    {
                        config.AppSettings.Settings[keyName].Value = $"{EncryptedKeyPrefix}{encryptedValue}";
                        config.Save(ConfigurationSaveMode.Modified);
                        ConfigurationManager.RefreshSection("appSettings");
                    }
                }
                catch
                {
                    // Ignore errors updating config
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error storing API key: {ex.Message}");
                MessageBox.Show($"Failed to securely store API key: {ex.Message}",
                    "Security Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Prompts the user to enter an API key
        /// </summary>
        private static Task<string> PromptForApiKeyAsync(string prompt)
        {
            return Task.Run(() =>
            {
                string result = string.Empty;

                // This has to run on the UI thread
                if (Application.OpenForms.Count > 0 && Application.OpenForms[0].InvokeRequired)
                {
                    Application.OpenForms[0].Invoke(new Action(() =>
                    {
                        using (var form = new Form())
                        {
                            form.Width = 450;
                            form.Height = 150;
                            form.Text = "API Key Required";
                            form.StartPosition = FormStartPosition.CenterScreen;
                            form.FormBorderStyle = FormBorderStyle.FixedDialog;
                            form.MaximizeBox = false;
                            form.MinimizeBox = false;

                            var label = new Label { Left = 20, Top = 20, Text = prompt, Width = 410 };
                            var textBox = new TextBox { Left = 20, Top = 50, Width = 410 };
                            var button = new Button { Text = "OK", Left = 185, Top = 80, Width = 80, DialogResult = DialogResult.OK };

                            form.Controls.Add(label);
                            form.Controls.Add(textBox);
                            form.Controls.Add(button);
                            form.AcceptButton = button;

                            if (form.ShowDialog() == DialogResult.OK)
                            {
                                result = textBox.Text;
                            }
                        }
                    }));
                }
                else
                {
                    // If no form is available, use console input as fallback
                    Console.WriteLine(prompt);
                    result = Console.ReadLine();
                }

                return result;
            });
        }

        /// <summary>
        /// Encrypts a value using Windows Data Protection API
        /// </summary>
        private static string EncryptValue(string value)
        {
            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            byte[] encryptedBytes = ProtectedData.Protect(valueBytes, _entropyBytes, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Decrypts a value using Windows Data Protection API
        /// </summary>
        private static string DecryptValue(string encryptedValue)
        {
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedValue);
                byte[] valueBytes = ProtectedData.Unprotect(encryptedBytes, _entropyBytes, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(valueBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Validates if an API key is working by making a test request
        /// </summary>
        public static async Task<bool> ValidateGoogleApiKeyAsync(string apiKey)
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    // Make a simple request to the Google Maps API
                    string url = $"https://maps.googleapis.com/maps/api/geocode/json?address=test&key={apiKey}";
                    var response = await client.GetStringAsync(url);

                    // Check if response contains an error related to the API key
                    bool isValid = !response.Contains("\"error_message\"") &&
                                   !response.Contains("API key") &&
                                   !response.Contains("invalid");

                    return isValid;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Clears all stored API keys (for debugging/testing)
        /// </summary>
        public static void ClearAllKeys()
        {
            try
            {
                if (File.Exists(_secureKeyFile))
                {
                    File.Delete(_secureKeyFile);
                }

                _cachedGoogleApiKey = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing API keys: {ex.Message}");
            }
        }
    }
}