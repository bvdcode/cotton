// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.Providers;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Provides cotton public email dependencies to server components.
    /// </summary>
    public class CottonPublicEmailProvider(
        HttpClient _httpClient,
        ILogger<CottonPublicEmailProvider> _logger)
    {
        /// <summary>
        /// Defines the Cotton Bridge base URL.
        /// </summary>
        public const string CottonBridgeBaseUrl = global::Cotton.Constants.CottonBridgeBaseUrl;
        /// <summary>
        /// Checks health.
        /// </summary>
        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                HealthResponse? response = await _httpClient.GetFromJsonAsync<HealthResponse>("health");
                return response is not null && response.Status == "Healthy";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check Cotton Bridge health.");
                return false;
            }
        }

        /// <summary>
        /// Sends email async.
        /// </summary>
        public async Task<bool> SendEmailAsync(
            Guid instanceId,
            EmailTemplate template,
            string serverUrl,
            string recipientEmail,
            string recipientName,
            string languageCode,
            Dictionary<string, string> parameters)
        {
            try
            {
                var request = new CottonBridgeEmailRequest
                {
                    Template = template.ToString(),
                    InstanceId = instanceId,
                    ServerUrl = serverUrl,
                    RecipientEmail = recipientEmail,
                    RecipientName = recipientName,
                    Language = MapLanguageCode(languageCode),
                    Parameters = parameters,
                };

                using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("email/send", request);
                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "Cotton Bridge returned {StatusCode} for {Template}: {Body}",
                        response.StatusCode,
                        template,
                        body);
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send {Template} email via Cotton Bridge.", template);
                return false;
            }
        }

        private static string MapLanguageCode(string code) => code switch
        {
            "ru" => "Russian",
            _ => "English",
        };

        private class CottonBridgeEmailRequest
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
}
