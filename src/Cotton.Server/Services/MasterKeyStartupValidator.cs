// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Storage.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    internal class MasterKeyStartupValidator(
        IStorageBackendProvider _backendProvider,
        IStreamCipher _cipher,
        CottonEncryptionSettings _encryptionSettings,
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrityVerifier,
        ILogger<MasterKeySentinelStore> _sentinelLogger,
        ILogger<MasterKeyStartupValidator> _logger)
    {
        private const int MaximumEvidenceCandidates = 3;

        public async Task ValidateAsync(CancellationToken cancellationToken = default)
        {
            IStorageBackend backend = _backendProvider.GetBackend();
            MasterKeySentinelStore sentinel = new(_sentinelLogger, backend);
            if (await sentinel.ExistsAsync())
            {
                EnsureValid(await sentinel.ValidateOrInitializeAsync(
                    _encryptionSettings,
                    cancellationToken));
                return;
            }

            int candidateCount = 0;
            bool evidenceValidated = false;
            await foreach (string storageKey in backend.ListAllKeysAsync(cancellationToken))
            {
                if (storageKey == MasterKeySentinelStore.SentinelStorageKey)
                {
                    continue;
                }

                candidateCount++;
                if (await CanDecryptAsync(backend, storageKey, cancellationToken))
                {
                    evidenceValidated = true;
                    break;
                }

                if (candidateCount >= MaximumEvidenceCandidates)
                {
                    break;
                }
            }

            if (!evidenceValidated)
            {
                evidenceValidated = await ValidateDatabaseEvidenceAsync(cancellationToken);
            }

            if (candidateCount > 0 && !evidenceValidated)
            {
                throw new InvalidOperationException(
                    "Master key does not match existing encrypted Cotton storage data.");
            }

            EnsureValid(await sentinel.ValidateOrInitializeAsync(
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

        private async Task<bool> ValidateDatabaseEvidenceAsync(CancellationToken cancellationToken)
        {
            User? user = await _dbContext.Users.FirstOrDefaultAsync(cancellationToken);
            if (user is not null)
            {
                _integrityVerifier.RequireValid(_dbContext, user, "startup.master-key");
                return true;
            }

            bool existingDataFound = await _dbContext.Nodes.AnyAsync(cancellationToken)
                || await _dbContext.FileManifests.AnyAsync(cancellationToken)
                || await _dbContext.Chunks.AnyAsync(cancellationToken)
                || await _dbContext.ServerSettings.AnyAsync(cancellationToken);
            if (existingDataFound)
            {
                throw new InvalidOperationException(
                    "Existing Cotton database data was found, but no integrity evidence was available to validate the configured master key.");
            }

            return false;
        }

        private static void EnsureValid(MasterKeySentinelResult result)
        {
            if (!result.Success)
            {
                throw new InvalidOperationException(
                    result.Error ?? "Master key sentinel validation failed.");
            }
        }
    }
}
