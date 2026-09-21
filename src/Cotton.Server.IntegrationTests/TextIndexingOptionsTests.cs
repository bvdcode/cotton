// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Models.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class TextIndexingOptionsTests
    {
        [TestCase(null, 4194304)]
        [TestCase("4096", 4096)]
        public void TextBudget_UsesConfiguredValueOr4MiBDefault(string? configured, int expected)
        {
            Dictionary<string, string?> values = [];
            if (configured is not null)
            {
                values["TextIndexing:MaxExtractedTextBytes"] = configured;
            }
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            ServiceCollection services = new();
            services.AddComputationServices();
            services.AddOptions<TextIndexingOptions>().Bind(configuration.GetSection(TextIndexingOptions.SectionName));
            using ServiceProvider provider = services.BuildServiceProvider();
            Assert.That(provider.GetRequiredService<IOptions<TextIndexingOptions>>().Value.MaxExtractedTextBytes,
                Is.EqualTo(expected));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void TextBudget_NonPositiveConfigurationIsRejected(int limit)
        {
            ServiceCollection services = new();
            services.AddComputationServices();
            services.Configure<TextIndexingOptions>(options => options.MaxExtractedTextBytes = limit);
            using ServiceProvider provider = services.BuildServiceProvider();
            Assert.Throws<OptionsValidationException>(() =>
            {
                _ = provider.GetRequiredService<IOptions<TextIndexingOptions>>().Value;
            });
        }

        [TestCase(null, 1048576)]
        [TestCase("4096", 4096)]
        public void StructuredFileLimit_UsesConfigurationOr1MiBDefault(string? configured, long expected)
        {
            Dictionary<string, string?> values = [];
            if (configured is not null)
            {
                values["TextIndexing:MaxStructuredFileBytes"] = configured;
            }
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            ServiceCollection services = new();
            services.AddComputationServices();
            services.AddOptions<TextIndexingOptions>().Bind(configuration.GetSection(TextIndexingOptions.SectionName));
            using ServiceProvider provider = services.BuildServiceProvider();
            Assert.That(provider.GetRequiredService<IOptions<TextIndexingOptions>>().Value.MaxStructuredFileBytes,
                Is.EqualTo(expected));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void StructuredFileLimit_NonPositiveConfigurationIsRejected(long limit)
        {
            ServiceCollection services = new();
            services.AddComputationServices();
            services.Configure<TextIndexingOptions>(options => options.MaxStructuredFileBytes = limit);
            using ServiceProvider provider = services.BuildServiceProvider();
            Assert.Throws<OptionsValidationException>(() =>
            {
                _ = provider.GetRequiredService<IOptions<TextIndexingOptions>>().Value;
            });
        }
    }
}
