// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Files;
using Cotton.Nodes;
using Cotton.Database;
using Cotton.Database.Models.Enums;
using Cotton.Server.Handlers.Files;
using Cotton.Server.IntegrationTests.Abstractions;
using Cotton.Server.IntegrationTests.Common;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Cotton.Server.IntegrationTests;

public class TrashRestoreEndpointsTests : IntegrationTestBase
{
    private const string OriginalParentPathMetadataKey = "originalParentPath";

    private TestAppFactory? _factory;
    private HttpClient? _client;

    [SetUp]
    public void SetUp()
    {
        var creator = DbContext.GetService<IRelationalDatabaseCreator>();
        creator.EnsureDeleted();
        creator.Create();

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = DatabaseName,
            Username = "postgres",
            Password = "postgres",
        };

        var overrides = new Dictionary<string, string?>
        {
            ["DatabaseSettings:Host"] = csb.Host,
            ["DatabaseSettings:Port"] = csb.Port.ToString(),
            ["DatabaseSettings:Database"] = csb.Database,
            ["DatabaseSettings:Username"] = csb.Username,
            ["DatabaseSettings:Password"] = csb.Password,
            ["MasterEncryptionKey"] = Convert.ToBase64String(Hasher.HashData(Encoding.UTF8.GetBytes("super"))),
            ["MasterEncryptionKeyId"] = "1",
            ["EncryptionThreads"] = "1",
            ["MaxChunkSizeBytes"] = "16777216",
            ["CipherChunkSizeBytes"] = "20971520",
            ["JwtSettings:Key"] = "T3wNTuKqmTXKjJKXHJRGUpG9sdrmpSX4",
        };

        _factory = new TestAppFactory(overrides);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task RestoreFile_FromTrash_ReturnsItToOriginalParentAndRemovesWrapper()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var folder = await CreateFolderAsync(root.Id, "docs");
        var file = await CreateFileAsync(folder.Id, "readme.txt", "hello");

        var delete = await _client!.DeleteAsync($"/api/v1/files/{file.Id}");
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        await using (var db = NewReadOnlyDbContext())
        {
            var trashed = await db.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == file.Id)
                .Select(x => new { x.NodeId })
                .SingleAsync();
            Assert.That(trashed.NodeId, Is.Not.EqualTo(folder.Id));
            Assert.That(await GetMetadataValueAsync("node_files", file.Id, OriginalParentPathMetadataKey), Is.EqualTo("docs"));
        }

        var outcome = await RestoreFileAsync(file.Id);

        Assert.That(outcome.Status, Is.EqualTo(RestoreStatus.Restored));
        Assert.That(outcome.OriginalParentPath, Is.EqualTo("docs"));
        Assert.That(outcome.RestoredFile?.Id, Is.EqualTo(file.Id));

        await using (var db = NewReadOnlyDbContext())
        {
            var restored = await db.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == file.Id)
                .Select(x => new { x.NodeId })
                .SingleAsync();
            Assert.That(restored.NodeId, Is.EqualTo(folder.Id));
            Assert.That(await MetadataContainsKeyAsync("node_files", file.Id, OriginalParentPathMetadataKey), Is.False);

            bool wrapperExists = await db.Nodes.AnyAsync(x => x.Type == NodeType.Trash
                && x.ParentId != null
                && x.Id != restored.NodeId);
            Assert.That(wrapperExists, Is.False);
        }
    }

    [Test]
    public async Task RestoreFile_WhenOriginalParentMissing_CanRecreateParentPath()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var folder = await CreateFolderAsync(root.Id, "docs");
        var file = await CreateFileAsync(folder.Id, "readme.txt", "hello");

        (await _client!.DeleteAsync($"/api/v1/files/{file.Id}")).EnsureSuccessStatusCode();
        (await _client.DeleteAsync($"/api/v1/layouts/nodes/{folder.Id}?skipTrash=true")).EnsureSuccessStatusCode();

        var missing = await RestoreFileAsync(file.Id);
        Assert.That(missing.Status, Is.EqualTo(RestoreStatus.ParentMissing));
        Assert.That(missing.MissingPath, Is.EqualTo("docs"));

        var restored = await RestoreFileAsync(file.Id, createMissingParents: true);
        Assert.That(restored.Status, Is.EqualTo(RestoreStatus.Restored));

        var newDocs = await ResolveDefaultNodeAsync("docs");
        Assert.That(newDocs, Is.Not.Null);
        await using var db = NewReadOnlyDbContext();
        var restoredFile = await db.NodeFiles
            .AsNoTracking()
            .Where(x => x.Id == file.Id)
            .Select(x => new { x.NodeId })
            .SingleAsync();
        Assert.That(restoredFile.NodeId, Is.EqualTo(newDocs!.Id));
    }

    [Test]
    public async Task RestoreFile_WithNameConflict_CanOverwriteByMovingConflictToTrash()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var folder = await CreateFolderAsync(root.Id, "docs");
        var original = await CreateFileAsync(folder.Id, "readme.txt", "original");

        (await _client!.DeleteAsync($"/api/v1/files/{original.Id}")).EnsureSuccessStatusCode();
        var blocker = await CreateFileAsync(folder.Id, "readme.txt", "blocker");

        var conflict = await RestoreFileAsync(original.Id);
        Assert.That(conflict.Status, Is.EqualTo(RestoreStatus.Conflict));
        Assert.That(conflict.ConflictKind, Is.EqualTo(RestoreConflictKind.File));

        var restored = await RestoreFileAsync(original.Id, overwrite: true);
        Assert.That(restored.Status, Is.EqualTo(RestoreStatus.Restored));

        await using var db = NewReadOnlyDbContext();
        var restoredFile = await db.NodeFiles
            .AsNoTracking()
            .Where(x => x.Id == original.Id)
            .Select(x => new { x.NodeId })
            .SingleAsync();
        var blockerFile = await db.NodeFiles
            .AsNoTracking()
            .Where(x => x.Id == blocker.Id)
            .Select(x => new { NodeType = x.Node.Type })
            .SingleAsync();

        Assert.That(restoredFile.NodeId, Is.EqualTo(folder.Id));
        Assert.That(blockerFile.NodeType, Is.EqualTo(NodeType.Trash));
    }

    [Test]
    public async Task RestoreNode_RestoresSubtreeTypesAndOriginalParent()
    {
        await AuthenticateAsync();
        var root = await GetRootAsync();
        var parent = await CreateFolderAsync(root.Id, "parent");
        var folder = await CreateFolderAsync(parent.Id, "project");
        var child = await CreateFolderAsync(folder.Id, "child");
        var file = await CreateFileAsync(child.Id, "note.txt", "nested");

        (await _client!.DeleteAsync($"/api/v1/layouts/nodes/{folder.Id}")).EnsureSuccessStatusCode();

        await using (var db = NewReadOnlyDbContext())
        {
            var trashedFolder = await db.Nodes
                .AsNoTracking()
                .Where(x => x.Id == folder.Id)
                .Select(x => new { x.Type })
                .SingleAsync();
            var trashedChild = await db.Nodes
                .AsNoTracking()
                .Where(x => x.Id == child.Id)
                .Select(x => new { x.Type })
                .SingleAsync();
            Assert.That(trashedFolder.Type, Is.EqualTo(NodeType.Trash));
            Assert.That(trashedChild.Type, Is.EqualTo(NodeType.Trash));
            Assert.That(await GetMetadataValueAsync("nodes", folder.Id, OriginalParentPathMetadataKey), Is.EqualTo("parent"));
            await AssertNoMixedNodeTypesAsync();
        }

        var restored = await RestoreNodeAsync(folder.Id);
        Assert.That(restored.Status, Is.EqualTo(RestoreStatus.Restored));
        Assert.That(restored.RestoredNode?.Id, Is.EqualTo(folder.Id));

        await using (var db = NewReadOnlyDbContext())
        {
            var restoredFolder = await db.Nodes
                .AsNoTracking()
                .Where(x => x.Id == folder.Id)
                .Select(x => new { x.ParentId, x.Type })
                .SingleAsync();
            var restoredChild = await db.Nodes
                .AsNoTracking()
                .Where(x => x.Id == child.Id)
                .Select(x => new { x.Type })
                .SingleAsync();
            var nestedFile = await db.NodeFiles
                .AsNoTracking()
                .Where(x => x.Id == file.Id)
                .Select(x => new { x.NodeId })
                .SingleAsync();

            Assert.That(restoredFolder.ParentId, Is.EqualTo(parent.Id));
            Assert.That(restoredFolder.Type, Is.EqualTo(NodeType.Default));
            Assert.That(restoredChild.Type, Is.EqualTo(NodeType.Default));
            Assert.That(nestedFile.NodeId, Is.EqualTo(child.Id));
            Assert.That(await MetadataContainsKeyAsync("nodes", folder.Id, OriginalParentPathMetadataKey), Is.False);
            await AssertNoMixedNodeTypesAsync();
        }
    }

    private async Task AuthenticateAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequestDto
            {
                Username = "testuser",
                Password = "testpassword",
            }),
        };
        request.Headers.Add("X-Forwarded-For", "8.8.8.8");

        var response = await _client!.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<TokenPairResponseDto>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    private async Task<NodeDto> GetRootAsync()
    {
        var root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
        return root!;
    }

    private async Task<NodeDto?> ResolveDefaultNodeAsync(string path)
    {
        var response = await _client!.GetAsync($"/api/v1/layouts/resolver/{Uri.EscapeDataString(path)}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NodeDto>();
    }

    private async Task<NodeDto> CreateFolderAsync(Guid parentId, string name)
    {
        var response = await _client!.PutAsJsonAsync(
            "/api/v1/layouts/nodes",
            new CreateNodeRequestDto { ParentId = parentId, Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NodeDto>())!;
    }

    private async Task<NodeFileManifestDto> CreateFileAsync(Guid nodeId, string name, string body)
    {
        string hash = await UploadChunkAsync(body);
        var fileReq = new CreateFileRequest
        {
            ChunkHashes = [hash],
            Name = name,
            ContentType = "application/octet-stream",
            Hash = hash,
            NodeId = nodeId,
        };
        var response = await _client!.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
        response.EnsureSuccessStatusCode();

        var children = await _client!.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{nodeId}/children");
        return children!.Files.Single(x => x.Name == name);
    }

    private async Task<string> UploadChunkAsync(string body)
    {
        byte[] content = Encoding.UTF8.GetBytes(body);
        string hash = Hasher.ToHexStringHash(Hasher.HashData(content));
        using var form = new MultipartFormDataContent
        {
            {
                new ByteArrayContent(content)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") },
                },
                "file",
                "chunk.bin"
            },
            { new StringContent(hash), "hash" },
        };

        var response = await _client!.PostAsync("/api/v1/chunks", form);
        response.EnsureSuccessStatusCode();
        return hash;
    }

    private async Task<RestoreOutcomeDto> RestoreFileAsync(
        Guid fileId,
        bool createMissingParents = false,
        bool overwrite = false)
    {
        var response = await _client!.PostAsJsonAsync(
            $"/api/v1/files/{fileId}/restore",
            new RestoreItemRequestDto
            {
                CreateMissingParents = createMissingParents,
                Overwrite = overwrite,
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RestoreOutcomeDto>())!;
    }

    private async Task<RestoreOutcomeDto> RestoreNodeAsync(
        Guid nodeId,
        bool createMissingParents = false,
        bool overwrite = false)
    {
        var response = await _client!.PostAsJsonAsync(
            $"/api/v1/layouts/nodes/{nodeId}/restore",
            new RestoreItemRequestDto
            {
                CreateMissingParents = createMissingParents,
                Overwrite = overwrite,
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RestoreOutcomeDto>())!;
    }

    private async Task AssertNoMixedNodeTypesAsync()
    {
        await using var db = NewReadOnlyDbContext();
        var mismatches = await db.Nodes
            .AsNoTracking()
            .Where(child => child.ParentId != null)
            .Join(
                db.Nodes.AsNoTracking(),
                child => child.ParentId!.Value,
                parent => parent.Id,
                (child, parent) => new
                {
                    child.Id,
                    ChildType = child.Type,
                    ParentType = parent.Type,
                })
            .Where(x => x.ChildType != x.ParentType)
            .ToListAsync();

        Assert.That(mismatches, Is.Empty);
    }

    private async Task<string?> GetMetadataValueAsync(string tableName, Guid id, string key)
    {
        string table = ValidateMetadataTable(tableName);
        string sql = $"""
            SELECT metadata -> @key
            FROM {table}
            WHERE id = @id;
            """;

        object? value = await ExecuteScalarAsync(sql, ("id", id), ("key", key));
        return value is DBNull ? null : (string?)value;
    }

    private async Task<bool> MetadataContainsKeyAsync(string tableName, Guid id, string key)
    {
        string table = ValidateMetadataTable(tableName);
        string sql = $"""
            SELECT COALESCE(metadata ? @key, false)
            FROM {table}
            WHERE id = @id;
            """;

        object? value = await ExecuteScalarAsync(sql, ("id", id), ("key", key));
        return value is bool contains && contains;
    }

    private async Task<object?> ExecuteScalarAsync(string sql, params (string Name, object Value)[] parameters)
    {
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = DatabaseName,
            Username = "postgres",
            Password = "postgres",
            Pooling = false,
        };

        await using var connection = new NpgsqlConnection(csb.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteScalarAsync();
    }

    private static string ValidateMetadataTable(string tableName) => tableName switch
    {
        "nodes" => "nodes",
        "node_files" => "node_files",
        _ => throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Unsupported metadata table."),
    };

    private CottonDbContext NewReadOnlyDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CottonDbContext>();
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = DatabaseName,
            Username = "postgres",
            Password = "postgres",
            Pooling = false,
        };
        optionsBuilder.UseNpgsql(csb.ConnectionString);
        return new CottonDbContext(optionsBuilder.Options);
    }
}
