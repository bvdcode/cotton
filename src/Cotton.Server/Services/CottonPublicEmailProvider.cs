using Cotton.Models.Enums;
using Cotton.Server.Providers;
using System.Net.Http.Json;

namespace Cotton.Server.Services
{
    public class CottonPublicEmailProvider : IDisposable
    {
        private const string GatewayBaseUrl = "https://cotton-gateway.splidex.com/api/v1/";
        private readonly HttpClient _httpClient;
        private readonly Guid _instanceId;
        private readonly ILogger<CottonPublicEmailProvider> _logger;

        public CottonPublicEmailProvider(
            SettingsProvider settingsProvider,
            ILogger<CottonPublicEmailProvider> logger)
        {
            _logger = logger;
            var settings = settingsProvider.GetServerSettings();
            _instanceId = settings.InstanceId;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(GatewayBaseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

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
            public string Template { get; set; } = null!;
            public Guid InstanceId { get; set; }
            public string ServerUrl { get; set; } = null!;
            public string RecipientEmail { get; set; } = null!;
            public string RecipientName { get; set; } = null!;
            public string Language { get; set; } = "English";
            public Dictionary<string, string> Parameters { get; set; } = [];
        }
    }

    public class HealthResponse
    {
        public string Status { get; set; } = null!;
        public Check[] Checks { get; set; } = [];
    }

    public class Check
    {
        public string Name { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string Description { get; set; } = null!;
    }
}
