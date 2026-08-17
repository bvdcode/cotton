// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Configuration;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Cotton.Server.Extensions
{
    public static class Http2TransportExtensions
    {
        public static WebApplicationBuilder ConfigureHttp2Transport(
            this WebApplicationBuilder builder)
        {
            Http2TransportOptions transportOptions = builder.Configuration
                .GetRequiredSection(Http2TransportOptions.SectionName)
                .Get<Http2TransportOptions>()
                ?? throw new InvalidOperationException(
                    $"Configuration section {Http2TransportOptions.SectionName} is invalid.");
            transportOptions.Validate();

            builder.WebHost.ConfigureKestrel(serverOptions =>
                Apply(serverOptions.Limits.Http2, transportOptions));

            return builder;
        }

        private static void Apply(
            Http2Limits limits,
            Http2TransportOptions options)
        {
            limits.InitialConnectionWindowSize = options.InitialConnectionWindowSize;
            limits.InitialStreamWindowSize = options.InitialStreamWindowSize;
        }
    }
}
