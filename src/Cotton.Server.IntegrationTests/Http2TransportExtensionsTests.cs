// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Models.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class Http2TransportExtensionsTests
    {
        [Test]
        public void ConfigureHttp2TransportAppliesConfiguredFlowControlWindows()
        {
            const int connectionWindowSize = 16 * 1024 * 1024;
            const int streamWindowSize = 1024 * 1024;
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Configuration[
                $"{Http2TransportOptions.SectionName}:{nameof(Http2TransportOptions.InitialConnectionWindowSize)}"] =
                connectionWindowSize.ToString();
            builder.Configuration[
                $"{Http2TransportOptions.SectionName}:{nameof(Http2TransportOptions.InitialStreamWindowSize)}"] =
                streamWindowSize.ToString();

            builder.ConfigureHttp2Transport();
            using WebApplication application = builder.Build();
            KestrelServerOptions options = application.Services
                .GetRequiredService<IOptions<KestrelServerOptions>>()
                .Value;

            Assert.Multiple(() =>
            {
                Assert.That(
                    options.Limits.Http2.InitialConnectionWindowSize,
                    Is.EqualTo(connectionWindowSize));
                Assert.That(
                    options.Limits.Http2.InitialStreamWindowSize,
                    Is.EqualTo(streamWindowSize));
            });
        }
    }
}
