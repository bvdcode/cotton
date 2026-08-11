// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Storage.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Validates a master key against the selected storage backend and database.
    /// </summary>
    internal class MasterKeyValidator(
        IStreamCipher _cipher,
        CottonEncryptionSettings _encryptionSettings,
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrityVerifier,
        ILogger<MasterKeySentinelStore> _sentinelLogger,
        ILogger<MasterKeyValidator> _logger)
    {
        private const int MaximumEvidenceCandidates = 3;

        public async Task<MasterKeySentinelResult> ValidateAsync(
            IStorageBackend backend,
            bool encryptedConfigurationValidated = false,
            CancellationToken cancellationToken = default)
        {
            MasterKeySentinelStore sentinel = new(_sentinelLogger, backend);
            if (await sentinel.ExistsAsync())
            {
                return EnsureValid(await sentinel.ValidateOrInitializeAsync(
                    _encryptionSettings,
                    cancellationToken));
            }

            if (encryptedConfigurationValidated)
            {
                return EnsureValid(await sentinel.ValidateOrInitializeAsync(
                    _encryptionSettings,
                    cancellationToken));
            }

            int candidateCount = 0;
            await foreach (string storageKey in backend.ListAllKeysAsync(cancellationToken))
            {
                if (storageKey == MasterKeySentinelStore.SentinelStorageKey)
                {
                    continue;
                }

                candidateCount++;
                if (await CanDecryptAsync(backend, storageKey, cancellationToken))
                {
                    return EnsureValid(await sentinel.ValidateOrInitializeAsync(
                        _encryptionSettings,
                        cancellationToken));
                }

                if (candidateCount >= MaximumEvidenceCandidates)
                {
                    break;
                }
            }

            if (candidateCount > 0)
            {
                throw new MasterKeyValidationException(
                    "Master key does not match existing encrypted Cotton storage data.");
            }

            await ValidateDatabaseEvidenceAsync(cancellationToken);
            return EnsureValid(await sentinel.ValidateOrInitializeAsync(
                _encryptionSettings,
                cancellationToken));
        }

        private async Task<bool> CanDecryptAsync(
            IStorageBackend backend,
            string storageKey,
            CancellationToken cancellationToken)
        {
            try
            {
                await using Stream encrypted = await backend.ReadAsync(storageKey);
                await _cipher.DecryptAsync(
                    encrypted,
                    Stream.Null,
                    ct: cancellationToken);
                return true;
            }
            catch (Exception ex) when (ex is CryptographicException
                or InvalidDataException
                or EndOfStreamException)
            {
                _logger.LogDebug(
                    ex,
                    "Stored master-key evidence {StorageKey} could not be decrypted.",
                    storageKey);
                return false;
            }
        }

        private async Task ValidateDatabaseEvidenceAsync(CancellationToken cancellationToken)
        {
            if (await EntityHasRowsAsync(_dbContext.Users, cancellationToken))
            {
                User user;
                try
                {
                    user = await _dbContext.Users.FirstAsync(cancellationToken);
                    _integrityVerifier.RequireValid(_dbContext, user, "startup.master-key");
                    return;
                }
                catch (PostgresException ex) when (IsMissingDatabaseShape(ex))
                {
                    _logger.LogWarning(
                        ex,
                        "Existing user data could not be materialized for master-key validation.");
                    throw new MasterKeyValidationException(
                        "Existing Cotton user data was found, but its database schema could not be used to validate the configured master key.",
                        ex);
                }
                catch (DatabaseIntegrityException ex)
                {
                    _logger.LogDebug(
                        ex,
                        "Existing user integrity evidence rejected the configured master key.");
                    throw new MasterKeyValidationException(
                        "Master key does not match the existing Cotton database integrity signature.",
                        ex);
                }
            }

            bool existingDataFound = await EntityHasRowsAsync(_dbContext.Nodes, cancellationToken)
                || await EntityHasRowsAsync(_dbContext.FileManifests, cancellationToken)
                || await EntityHasRowsAsync(_dbContext.Chunks, cancellationToken)
                || await EntityHasRowsAsync(_dbContext.ServerSettings, cancellationToken);
            if (existingDataFound)
            {
                throw new MasterKeyValidationException(
                    "Existing Cotton database data was found, but no integrity evidence was available to validate the configured master key.");
            }
        }

        private async Task<bool> EntityHasRowsAsync<TEntity>(
            IQueryable<TEntity> query,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            try
            {
                return await query.AsNoTracking().AnyAsync(cancellationToken);
            }
            catch (PostgresException ex) when (IsMissingDatabaseShape(ex))
            {
                _logger.LogDebug(
                    ex,
                    "Database object is not available while checking master-key evidence.");
                return false;
            }
        }

        private static bool IsMissingDatabaseShape(PostgresException ex)
        {
            return ex.SqlState == PostgresErrorCodes.InvalidCatalogName
                || ex.SqlState == PostgresErrorCodes.UndefinedTable
                || ex.SqlState == PostgresErrorCodes.UndefinedColumn
                || ex.SqlState == PostgresErrorCodes.UndefinedObject;
        }

        private static MasterKeySentinelResult EnsureValid(MasterKeySentinelResult result)
        {
            if (!result.Success)
            {
                throw new MasterKeyValidationException(
                    result.Error ?? "Master key sentinel validation failed.");
            }

            return result;
        }
    }
}
