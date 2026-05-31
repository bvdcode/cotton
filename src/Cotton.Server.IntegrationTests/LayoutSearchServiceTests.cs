// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Extensions;
using Cotton.Server.Services.Search;
using Microsoft.EntityFrameworkCore;
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
    public void MergeDuplicateHits_KeepsBestScorePerEntity()
    {
        Guid fileId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid nodeId = Guid.Parse("22222222-3333-4444-5555-666666666666");
        Guid parentNodeId = Guid.Parse("33333333-4444-5555-6666-777777777777");

        LayoutSearchHit[] hits =
        [
            new()
            {
                Kind = LayoutSearchHitKind.File,
                Id = fileId,
                NodeIdForPath = parentNodeId,
                Name = "report-low.md",
                NameKey = "report-low.md",
                Score = 0.4,
            },
            new()
            {
                Kind = LayoutSearchHitKind.File,
                Id = fileId,
                NodeIdForPath = parentNodeId,
                Name = "report-high.md",
                NameKey = "report-high.md",
                Score = 0.9,
            },
            new()
            {
                Kind = LayoutSearchHitKind.Node,
                Id = nodeId,
                NodeIdForPath = nodeId,
                Name = "Reports",
                NameKey = "reports",
                Score = 0.5,
            },
        ];

        IReadOnlyList<LayoutSearchHit> merged = LayoutSearchHitMerger.MergeDuplicateHits(hits);
        LayoutSearchHit fileHit = merged.Single(x => x.Kind == LayoutSearchHitKind.File);

        Assert.Multiple(() =>
        {
            Assert.That(merged, Has.Count.EqualTo(2));
            Assert.That(fileHit.Id, Is.EqualTo(fileId));
            Assert.That(fileHit.Score, Is.EqualTo(0.9));
            Assert.That(fileHit.Name, Is.EqualTo("report-high.md"));
        });
    }

    [Test]
    public void MergeDuplicateHits_QueryableTranslatesForNameProvider()
    {
        const string connectionString = "Host=localhost;Database=cotton_translation_test;Username=postgres;Password=postgres";

        DbContextOptions<CottonDbContext> options = new DbContextOptionsBuilder<CottonDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        using CottonDbContext dbContext = new(options);

        LayoutSearchRequest request = new(
            UserId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
            LayoutId: Guid.Parse("22222222-3333-4444-5555-666666666666"),
            Query: "demo",
            Page: 1,
            PageSize: 20);
        LayoutSearchCriteria criteria = LayoutSearchCriteriaBuilder.Build(request.Query);
        NameLayoutSearchProvider provider = new(dbContext);

        IQueryable<LayoutSearchHit> query = LayoutSearchHitMerger
            .MergeDuplicateHits(provider.BuildHitsQuery(new LayoutSearchProviderContext(request, criteria)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Kind)
            .ThenBy(x => x.NameKey)
            .ThenBy(x => x.Id)
            .Skip(0)
            .Take(request.PageSize);

        Assert.That(query.ToQueryString(), Does.Contain("GROUP BY"));
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
