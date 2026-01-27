using Cotton.Shared;
using Cotton.Shared.Models;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Cotton.SDK
{
    public class CottonCloudClient : ICottonClient, IDisposable
    {
        private readonly HttpClient _http;

        public CottonCloudClient(string serverUrl)
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(serverUrl)
            };
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        public async Task<PublicServerInfo> GetServerInfoAsync()
        {
            return await _http.GetFromJsonAsync<PublicServerInfo>(Routes.V1.Server + "/info")
                ?? throw new InvalidOperationException("Failed to retrieve server info.");
        }
    }
}
