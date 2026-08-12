// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.Providers;

namespace Cotton.Server.Services
{
    public class CottonPublicEmailProvider(
        HttpClient _httpClient,
        ILogger<CottonPublicEmailProvider> _logger)
    {
        private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(10);

        public const string CottonBridgeBaseUrl = global::Cotton.Constants.CottonBridgeBaseUrl;
        public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(HealthTimeout);
            try
            {
                HealthResponse? response = await _httpClient.GetFromJsonAsync<HealthResponse>(
                    "health",
                    timeout.Token);
                return response is not null && response.Status == "Healthy";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check Cotton Bridge health.");
                return false;
            }
        }

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
            public string Template { get; set; } = null!;

            public Guid InstanceId { get; set; }

            public string ServerUrl { get; set; } = null!;

            public string RecipientEmail { get; set; } = null!;

            public string RecipientName { get; set; } = null!;

            public string Language { get; set; } = "English";
            public Dictionary<string, string> Parameters { get; set; } = [];
        }
    }
}
