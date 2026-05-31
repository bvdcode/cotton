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
            Assert.That(criteria.HasVectorSearchText, Is.True);
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
            Assert.That(criteria.HasOnlyIds, Is.False);
            Assert.That(criteria.HasVectorSearchText, Is.True);
            Assert.That(criteria.TextTokens.Select(x => x.NameKey), Is.EqualTo(new[] { "report", "final" }));
        });
    }

    [Test]
    public void BuildCriteria_RecognizesGuidOnlyQueries()
    {
        Guid id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        LayoutSearchCriteria criteria = LayoutSearchCriteriaBuilder.Build(id.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(criteria.IdQueries, Is.EqualTo(new[] { id }));
            Assert.That(criteria.HasText, Is.False);
            Assert.That(criteria.HasOnlyIds, Is.True);
            Assert.That(criteria.HasVectorSearchText, Is.False);
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
    public void VectorProvider_CanSearchOnlyNaturalLanguageText()
    {
        NoOpVectorLayoutSearchProvider provider = new(null!);
        Guid id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        Assert.Multiple(() =>
        {
            Assert.That(provider.CanSearch(LayoutSearchCriteriaBuilder.Build(id.ToString())), Is.False);
            Assert.That(provider.CanSearch(LayoutSearchCriteriaBuilder.Build("123456")), Is.False);
            Assert.That(provider.CanSearch(LayoutSearchCriteriaBuilder.Build("report 123456")), Is.True);
            Assert.That(provider.CanSearch(LayoutSearchCriteriaBuilder.Build("quarterly report")), Is.True);
        });
    }

    [Test]
    public void ExactIdentifierScore_IsCertainMatch()
    {
        Assert.That(LayoutSearchScores.ExactIdentifier, Is.EqualTo(1.0));
    }

    [Test]
    public void AddLayoutSearchServices_RegistersServiceAndProviders()
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
            Assert.That(services.Any(x =>
                x.ServiceType == typeof(ILayoutSearchProvider)
                && x.ImplementationType == typeof(NoOpVectorLayoutSearchProvider)), Is.True);
        });
    }
}
