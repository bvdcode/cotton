// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Handlers.Bridge;
using Cotton.Server.Models.Bridge;
using EasyExtensions.Mediator;
using System.Net.Http.Headers;

namespace Cotton.Server.Services.Bridge
{
    public class BridgeCredentialProvider(
        IServiceScopeFactory scopes,
        IHttpClientFactory clients) : IBridgeCredentialProvider, IDisposable
    {
        public const string RegistrationClientName = "CottonBridgeRegistration";
        public const string InstanceIdHeader = "X-Cotton-Instance-Id";
        private readonly SemaphoreSlim _registrationGate = new(1, 1);
        private BridgeCredential? _registered;

        public async Task<BridgeCredential> GetAsync(CancellationToken cancellationToken)
        {
            BridgeCredential? cached = Volatile.Read(ref _registered);
            if (cached is not null)
            {
                return cached;
            }

            await _registrationGate.WaitAsync(cancellationToken);
            try
            {
                if (_registered is not null)
                {
                    return _registered;
                }

                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                BridgeCredential credential = await mediator.Send(new GetBridgeCredentialRequest(), cancellationToken);
                using HttpClient client = clients.CreateClient(RegistrationClientName);
                using HttpRequestMessage request = new(HttpMethod.Post, "instances");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Token);
                request.Headers.Add(InstanceIdHeader, credential.InstanceId.ToString());
                using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                Volatile.Write(ref _registered, credential);
                return credential;
            }
            finally
            {
                _registrationGate.Release();
            }
        }

        public void Dispose()
        {
            _registrationGate.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
