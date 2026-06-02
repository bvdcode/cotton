// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Data.Common;
using Cotton.Contracts.Auth;
using Cotton.Sdk.Auth;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Sync.Cli.Storage;

internal sealed class SqliteCliStateStore : ICottonTokenStore
{
    private const string AccessTokenKey = "access_token";
    private const string RefreshTokenKey = "refresh_token";
    private const string ServerBaseAddressKey = "server_base_address";
    private readonly string _databasePath;

    public SqliteCliStateStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        EnsureDirectoryExists();
        await using CliStateDbContext context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TokenPairDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using CliStateDbContext context = CreateContext();
        Dictionary<string, string> values = await context.StateItems
            .AsNoTracking()
            .Where(item => item.Key == AccessTokenKey || item.Key == RefreshTokenKey)
            .ToDictionaryAsync(item => item.Key, item => item.Value, cancellationToken)
            .ConfigureAwait(false);
        return values.TryGetValue(AccessTokenKey, out string? accessToken)
            && values.TryGetValue(RefreshTokenKey, out string? refreshToken)
            && !string.IsNullOrWhiteSpace(accessToken)
            && !string.IsNullOrWhiteSpace(refreshToken)
                ? new TokenPairDto { AccessToken = accessToken, RefreshToken = refreshToken }
                : null;
    }

    public async Task SaveAsync(TokenPairDto tokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using CliStateDbContext context = CreateContext();
        await UpsertAsync(context, AccessTokenKey, tokens.AccessToken, cancellationToken).ConfigureAwait(false);
        await UpsertAsync(context, RefreshTokenKey, tokens.RefreshToken, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using CliStateDbContext context = CreateContext();
        await context.StateItems
            .Where(item => item.Key == AccessTokenKey || item.Key == RefreshTokenKey)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveServerBaseAddressAsync(Uri serverBaseAddress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serverBaseAddress);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using CliStateDbContext context = CreateContext();
        await UpsertAsync(context, ServerBaseAddressKey, serverBaseAddress.ToString().TrimEnd('/'), cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Uri?> GetServerBaseAddressAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using CliStateDbContext context = CreateContext();
        string? value = await context.StateItems
            .AsNoTracking()
            .Where(item => item.Key == ServerBaseAddressKey)
            .Select(item => item.Value)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(value) ? null : new Uri(value, UriKind.Absolute);
    }

    private async Task UpsertAsync(CliStateDbContext context, string key, string value, CancellationToken cancellationToken)
    {
        CliStateItem? item = await context.StateItems.FindAsync([key], cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            context.StateItems.Add(new CliStateItem { Key = key, Value = value });
            return;
        }

        item.Value = value;
    }

    private CliStateDbContext CreateContext()
    {
        var connectionString = new DbConnectionStringBuilder
        {
            ["Data Source"] = _databasePath,
        }.ToString();
        DbContextOptions<CliStateDbContext> options = new DbContextOptionsBuilder<CliStateDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new CliStateDbContext(options);
    }

    private void EnsureDirectoryExists()
    {
        string? directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
