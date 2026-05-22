// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Models.Enums;
using Cotton.Server.Providers;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Provides cotton public email dependencies to server components.
    /// </summary>
    public class CottonPublicEmailProvider : IDisposable
    {
        /// <summary>
        /// Defines the gateway base URL.
        /// </summary>
        public const string GatewayBaseUrl = "https://cotton-gateway.splidex.com/api/v1/";
        private readonly HttpClient _httpClient;
        private readonly Guid _instanceId;
        private readonly ILogger<CottonPublicEmailProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CottonPublicEmailProvider"/> type.
        /// </summary>
        public CottonPublicEmailProvider(
            IServiceProvider serviceProvider,
            ILogger<CottonPublicEmailProvider> logger)
        {
            _logger = logger;
            using var scope = serviceProvider.CreateScope();
            var settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider>();
            var settings = settingsProvider.GetServerSettings();
            _instanceId = settings.InstanceId;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(GatewayBaseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        /// <summary>
        /// Checks health.
        /// </summary>
        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<HealthResponse>("health");
                return response != null && response.Status == "Healthy";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check gateway health.");
                return false;
            }
        }

        /// <summary>
        /// Sends email async.
        /// </summary>
        public async Task<bool> SendEmailAsync(
            EmailTemplate template,
            string serverUrl,
            string recipientEmail,
            string recipientName,
            string languageCode,
            Dictionary<string, string> parameters)
        {
            try
            {
                var request = new GatewayEmailRequest
                {
                    Template = template.ToString(),
                    InstanceId = _instanceId,
                    ServerUrl = serverUrl,
                    RecipientEmail = recipientEmail,
                    RecipientName = recipientName,
                    Language = MapLanguageCode(languageCode),
                    Parameters = parameters,
                };

                var response = await _httpClient.PostAsJsonAsync("email/send", request);
                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "Gateway returned {StatusCode} for {Template}: {Body}",
                        response.StatusCode,
                        template,
                        body);
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send {Template} email via gateway.", template);
                return false;
            }
        }

        /// <summary>
        /// Releases resources held by this instance.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _httpClient.Dispose();
        }

        private static string MapLanguageCode(string code) => code switch
        {
            "ru" => "Russian",
            _ => "English",
        };

        private sealed class GatewayEmailRequest
        {
            /// <summary>
            /// Gets or sets the template.
            /// </summary>
            public string Template { get; set; } = null!;
            /// <summary>
            /// Gets or sets the instance id.
            /// </summary>
            public Guid InstanceId { get; set; }
            /// <summary>
            /// Gets or sets the server URL.
            /// </summary>
            public string ServerUrl { get; set; } = null!;
            /// <summary>
            /// Gets or sets the recipient email.
            /// </summary>
            public string RecipientEmail { get; set; } = null!;
            /// <summary>
            /// Gets or sets the recipient name.
            /// </summary>
            public string RecipientName { get; set; } = null!;
            /// <summary>
            /// Gets or sets the language.
            /// </summary>
            public string Language { get; set; } = "English";
            public Dictionary<string, string> Parameters { get; set; } = [];
        }
    }

    /// <summary>
    /// Represents health response.
    /// </summary>
    public class HealthResponse
    {
        /// <summary>
        /// Gets or sets the operation status.
        /// </summary>
        public string Status { get; set; } = null!;
        /// <summary>
        /// Gets or sets the checks.
        /// </summary>
        public Check[] Checks { get; set; } = [];
    }

    /// <summary>
    /// Represents check.
    /// </summary>
    public class Check
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = null!;
        /// <summary>
        /// Gets or sets the operation status.
        /// </summary>
        public string Status { get; set; } = null!;
        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = null!;
    }
}
