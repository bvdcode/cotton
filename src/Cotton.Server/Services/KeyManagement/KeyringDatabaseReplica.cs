// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Database replica for small encrypted keyring system objects.
/// </summary>
internal sealed class KeyringDatabaseReplica(CottonDbContext _dbContext, string? _name = null) : IKeyringObjectReplica
{
    public string Name { get; } = string.IsNullOrWhiteSpace(_name) ? "database" : _name;

    public async Task WriteAsync(string name, byte[] bytes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(bytes);

        DateTime now = DateTime.UtcNow;
        KeyringObject? row = await _dbContext.KeyringObjects.FindAsync([name], cancellationToken);
        if (row is null)
        {
            row = new KeyringObject
            {
                Name = name,
                CreatedAt = now
            };
            await _dbContext.KeyringObjects.AddAsync(row, cancellationToken);
        }

        row.Bytes = bytes.ToArray();
        row.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<byte[]?> TryReadAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return await _dbContext.KeyringObjects
            .AsNoTracking()
            .Where(x => x.Name == name)
            .Select(x => x.Bytes)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async IAsyncEnumerable<string> ListNamesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (string name in _dbContext.KeyringObjects
            .AsNoTracking()
            .Select(x => x.Name)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            yield return name;
        }
    }
}
