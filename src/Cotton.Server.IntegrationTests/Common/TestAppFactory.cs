// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Cotton.Server.IntegrationTests.Common;

public class TestAppFactory : WebApplicationFactory<Cotton.Server.Program>
{
    private readonly IDictionary<string, string?> _overrides;

    public TestAppFactory(IDictionary<string, string?> overrides)
    {
        _overrides = overrides;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var dict = new Dictionary<string, string?>(_overrides)
            {
                ["Quartz:Enabled"] = "false" // disable Quartz for tests
            };
            config.AddInMemoryCollection(dict!);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Ensure host is created per test without cross-test reuse
        builder.UseEnvironment("IntegrationTests");
        return base.CreateHost(builder);
    }
}
