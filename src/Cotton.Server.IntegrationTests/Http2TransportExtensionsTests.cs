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
        [TestCase(16 * 1024 * 1024, 1024 * 1024, 8)]
        [TestCase(2 * 1024 * 1024, 1024 * 1024, 1)]
        [TestCase(65_535, 65_535, 1)]
        [TestCase(int.MaxValue, 65_535, 100)]
        public void ConfigureHttp2TransportAppliesConfiguredFlowControlWindows(
            int connectionWindowSize,
            int streamWindowSize,
            int maxStreams)
        {
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
                Assert.That(options.Limits.Http2.MaxStreamsPerConnection, Is.EqualTo(maxStreams));
            });
        }
    }
}
