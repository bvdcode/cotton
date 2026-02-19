using Cotton.Server.Providers;

namespace Cotton.Server.Services
{
    public class CottonPublicEmailProvider : IDisposable
    {
        // TODO: Don't forget to check a telemetry checkbox
        private const string BaseUrl = "https://cotton-gateway.splidex.com/api/v1/";
        private readonly HttpClient _httpClient;
        private readonly Guid _instanceId;

        public CottonPublicEmailProvider(SettingsProvider _settingsProvider)
        {
            var settings = _settingsProvider.GetServerSettings();
            _instanceId = settings.InstanceId;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        public async Task<bool> CheckHealthAsync()
        {
            var response = await _httpClient.GetFromJsonAsync<HealthResponse>("health");
            return response != null && response.Status == "Healthy";
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _httpClient.Dispose();
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
