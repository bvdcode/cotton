using System;
using System.Net.Http;

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
    }
}
