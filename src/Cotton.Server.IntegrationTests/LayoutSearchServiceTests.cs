// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Services.Search;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests;

public sealed class LayoutSearchServiceTests
{
    [Test]
    public void BuildCriteria_SplitsFileNameTermsForOrderIndependentSearch()
    {
        LayoutSearchCriteria criteria = LayoutSearchCriteriaBuilder.Build("Пупкин Вася");

        Assert.Multiple(() =>
        {
            Assert.That(criteria.HasText, Is.True);
            Assert.That(criteria.HasIds, Is.False);
            Assert.That(criteria.HasMultipleTextTokens, Is.True);
            Assert.That(criteria.TextTokens.Select(x => x.NameKey), Is.EqualTo(new[] { "пупкин", "вася" }));
            Assert.That(criteria.TextTokens.Select(x => x.ContainsPattern), Is.EqualTo(new[] { "%пупкин%", "%вася%" }));
        });
    }

    [Test]
    public void BuildCriteria_ExtractsGuidsAndKeepsRemainingTextTokens()
    {
        Guid id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        LayoutSearchCriteria criteria = LayoutSearchCriteriaBuilder.Build($"{id} report final");

        Assert.Multiple(() =>
        {
            Assert.That(criteria.IdQueries, Is.EqualTo(new[] { id }));
            Assert.That(criteria.TextTokens.Select(x => x.NameKey), Is.EqualTo(new[] { "report", "final" }));
        });
    }

    [Test]
    public void BuildCriteria_EscapesLikeWildcards()
    {
        LayoutSearchCriteria criteria = LayoutSearchCriteriaBuilder.Build("100%_ready");

        Assert.Multiple(() =>
        {
            Assert.That(criteria.ContainsPattern, Is.EqualTo("%100\\%\\_ready%"));
            Assert.That(criteria.PrefixPattern, Is.EqualTo("100\\%\\_ready%"));
        });
    }

    [Test]
    public void NameProvider_CanSearchTextOrIds()
    {
        NameLayoutSearchProvider provider = new(null!);
        Guid id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        LayoutSearchCriteria textCriteria = LayoutSearchCriteriaBuilder.Build("report");
        LayoutSearchCriteria idCriteria = LayoutSearchCriteriaBuilder.Build(id.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(provider.CanSearch(textCriteria), Is.True);
            Assert.That(provider.CanSearch(idCriteria), Is.True);
        });
    }

    [Test]
    public void AddLayoutSearchServices_RegistersServiceAndNameProvider()
    {
        ServiceCollection services = new();

        services.AddLayoutSearchServices();

        Assert.Multiple(() =>
        {
            Assert.That(services.Any(x =>
                x.ServiceType == typeof(ILayoutSearchService)
                && x.ImplementationType == typeof(LayoutSearchService)), Is.True);
            Assert.That(services.Any(x =>
                x.ServiceType == typeof(ILayoutSearchProvider)
                && x.ImplementationType == typeof(NameLayoutSearchProvider)), Is.True);
        });
    }
}
