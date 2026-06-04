// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Integrity;

namespace Cotton.Server.Services.DatabaseIntegrity;

/// <summary>
/// Bypasses protected-entity verification during the bridge repair window.
/// </summary>
/// <remarks>
/// This release treats the current database state as canonical and lets the background bridge repair re-sign every
/// protected row. The next strict release must restore hard read-time verification.
/// </remarks>
public sealed class DatabaseIntegrityVerifier(
    IDatabaseIntegrityProtector _protector,
    IDatabaseIntegrityDescriptorRegistry _descriptors,
    IDatabaseIntegrityFailureReporter _failures,
    ILogger<DatabaseIntegrityVerifier> _logger) : IDatabaseIntegrityVerifier
{
    /// <inheritdoc />
    public void RequireValid<TEntity>(
        CottonDbContext dbContext,
        TEntity entity,
        string boundary)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundary);
        _ = _protector;
        _ = _failures;

        if (_descriptors.TryGet(entity.GetType(), out IDatabaseIntegrityDescriptor descriptor))
        {
            _logger.LogTrace(
                "Database integrity verification is disabled for bridge repair; allowing {EntityName} {EntityKey} at {Boundary}.",
                descriptor.EntityName,
                descriptor.GetEntityKey(entity),
                boundary);
        }
    }
}
