// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Jobs;
using Cotton.Server.Services;
using Cotton.Server.Services.Bridge;
using Cotton.Server.Services.Computation;

namespace Cotton.Server.Extensions
{
    public static class CottonBridgeServiceCollectionExtensions
    {
        public const string ServicesClientName = "CottonBridgeServices";
        public const string ComputationClientName = "CottonBridgeComputation";

        public static IServiceCollection AddCottonBridgeClients(this IServiceCollection services)
        {
            services.AddSingleton<IBridgeCredentialProvider, BridgeCredentialProvider>();
            services.AddTransient<BridgeAuthenticationHandler>();
            services.AddHttpClient(BridgeCredentialProvider.RegistrationClientName, ConfigureClient)
                .ConfigurePrimaryHttpMessageHandler(CreateHandler);
            services.AddHttpClient<CottonPublicEmailProvider>(ConfigureClient)
                .ConfigurePrimaryHttpMessageHandler(CreateHandler)
                .AddHttpMessageHandler<BridgeAuthenticationHandler>();
            services.AddHttpClient(ServicesClientName, ConfigureClient)
                .ConfigurePrimaryHttpMessageHandler(CreateHandler)
                .AddHttpMessageHandler<BridgeAuthenticationHandler>();
            services.AddHttpClient(ComputationClientName, client =>
                {
                    ConfigureClient(client);
                    client.Timeout = TeiClient.RequestTimeout;
                })
                .ConfigurePrimaryHttpMessageHandler(CreateHandler)
                .AddHttpMessageHandler<BridgeAuthenticationHandler>();
            services.AddHttpClient(CollectPerformanceJob.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(CreateHandler)
                .AddHttpMessageHandler<BridgeAuthenticationHandler>();
            return services;
        }

        private static void ConfigureClient(HttpClient client)
        {
            client.BaseAddress = new Uri(global::Cotton.Constants.CottonBridgeBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        }

        private static HttpClientHandler CreateHandler()
        {
            return new HttpClientHandler { AllowAutoRedirect = false };
        }
    }
}
