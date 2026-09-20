// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services.Search;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System.Net;

namespace Cotton.Server.IntegrationTests
{
    public partial class VectorLayoutSearchTests
    {
        [Test]
        public async Task OrdinarySearch_WorksWithoutPgvectorOrEmbeddingCalls()
        {
            NodeFile file = AddFile("report.pdf", []);
            await DbContext.SaveChangesAsync();
            PagedResult<SearchResultDto> result = await Search(deep: false);
            Assert.That(result.Payload.Files.Single().Id, Is.EqualTo(file.Id));
            Assert.That(_worker.InfoCalls + _worker.EmbedCalls, Is.Zero);
            Assert.That(await DbContext.Database.IsExtensionInstalledAsync("vector"), Is.False);
        }

        [Test]
        public async Task DeepSearch_WithoutIndexedFiles_DoesNotRequirePgvectorOrAWorker()
        {
            AddFile("report.pdf", []);
            await DbContext.SaveChangesAsync();
            PagedResult<SearchResultDto> result = await Search();
            Assert.That(result.TotalCount, Is.Zero);
            Assert.That(_worker.InfoCalls + _worker.EmbedCalls, Is.Zero);
            Assert.That(await DbContext.Database.IsExtensionInstalledAsync("vector"), Is.False);
        }

        [Test]
        public async Task DeepSearch_RanksByBestFragment_DeduplicatesFiles_AndPaginatesWithPaths()
        {
            NodeFile far = AddFile("aardvark.pdf", [Direction(0.8f, 0.6f)]);
            NodeFile near = AddFile("zulu.pdf", [Direction(1, 0), Direction(0, 1)]);
            NodeFile copy = new() { FileManifest = near.FileManifest, Node = _folder, Owner = _folder.Owner };
            copy.SetName("copy.pdf");
            DbContext.NodeFiles.Add(copy);
            await PrepareIndex();
            PagedResult<SearchResultDto> first = await Search(pageSize: 2);
            PagedResult<SearchResultDto> second = await Search(page: 2, pageSize: 2);
            Assert.Multiple(() =>
            {
                Assert.That(first.TotalCount, Is.EqualTo(3));
                Assert.That(first.Payload.Files.Select(file => file.Id), Is.EqualTo(new[] { copy.Id, near.Id }));
                Assert.That(second.TotalCount, Is.EqualTo(3));
                Assert.That(second.Payload.Files.Single().Id, Is.EqualTo(far.Id));
                Assert.That(first.Payload.FilePaths[near.Id], Is.EqualTo("/files/zulu.pdf"));
                Assert.That(first.Payload.Nodes, Is.Empty);
            });
            Assert.That((await Search(page: 3, pageSize: 2)).Payload.Files, Is.Empty);
        }

        [Test]
        public async Task DeepSearch_RestrictsOwnerLayoutTrashHistoryAndIndexVersion()
        {
            NodeFile visible = AddFile("visible.pdf", [Direction(0.8f, 0.6f)]);
            User other = new() { Username = "other", PasswordPhc = "phc", WebDavTokenPhc = "token" };
            Node otherOwner = CreateFolder(other, new Layout { Owner = other, IsActive = true });
            Node otherLayout = CreateFolder(_folder.Owner, new Layout { Owner = _folder.Owner, IsActive = true });
            Node trash = CreateFolder(_folder.Owner, _folder.Layout, NodeType.Trash);
            AddFile("private.pdf", [Direction(1, 0)], otherOwner);
            AddFile("other-layout.pdf", [Direction(1, 0)], otherLayout);
            AddFile("trash.pdf", [Direction(1, 0)], trash);
            AddFile("history.pdf", [Direction(1, 0)]).OriginalNodeFileId = Guid.NewGuid();
            AddFile("other-version.pdf", [[1, 0]], version: VectorIndexDefinition.Version + 1);
            await PrepareIndex();
            PagedResult<SearchResultDto> result = await Search();
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Payload.Files.Single().Id, Is.EqualTo(visible.Id));
        }

        [Test]
        public async Task DeepSearch_MoreThanFortyFiles_RemainAvailableThroughPagination()
        {
            for (int i = 0; i < 90; i++)
            {
                AddFile($"report-{i:D3}.pdf", [Direction(1, (i + 1) / 100f)]);
            }
            await PrepareIndex();
            PagedResult<SearchResultDto> result = await Search(page: 3, pageSize: 40);
            Assert.That(result.TotalCount, Is.EqualTo(90));
            Assert.That(result.Payload.Files.Count(), Is.EqualTo(10));
        }

        [Test]
        public async Task DeepSearch_IndexNotReady_DoesNotCallWorker()
        {
            AddFile("report.pdf", [Direction(1, 0)]);
            await DbContext.SaveChangesAsync();
            Assert.ThrowsAsync<WebApiException>(() => Search());
            Assert.That(_worker.InfoCalls + _worker.EmbedCalls, Is.Zero);
        }

        [Test]
        public async Task DeepSearch_FiltersOwnershipBeforeLimitingNearestFragments()
        {
            User other = new() { Username = "other", PasswordPhc = "phc", WebDavTokenPhc = "token" };
            Node otherFolder = CreateFolder(other, new Layout { Owner = other, IsActive = true });
            for (int i = 0; i < 1200; i++)
            {
                AddFile($"private-{i}.pdf", [Direction(1, i / 10000f)], otherFolder);
            }
            for (int i = 0; i < 60; i++)
            {
                AddFile($"owned-{i}.pdf", [Direction(0.6f, (i + 800) / 1000f)]);
            }
            await PrepareIndex();
            PagedResult<SearchResultDto> result = await Search(page: 3, pageSize: 20);
            Assert.That(result.TotalCount, Is.EqualTo(60));
            Assert.That(result.Payload.Files.Count(), Is.EqualTo(20));
            Assert.That(result.Payload.Files.All(file => file.OwnerId == _folder.OwnerId), Is.True);
        }

        [Test]
        public async Task DeepSearch_WorkerFailure_IsReturnedAsAnError()
        {
            AddFile("report.pdf", [Direction(1, 0)]);
            await PrepareIndex();
            _worker.StatusCode = HttpStatusCode.ServiceUnavailable;
            Assert.ThrowsAsync<HttpRequestException>(() => Search());
        }
    }
}
