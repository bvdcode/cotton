// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Storage.Processors;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Linq.Expressions;

namespace Cotton.Server.Providers
{
    public class SettingsProvider(
        CottonDbContext _dbContext,
        ServerSettingsCache _cache,
        IStorageBackendTypeCache? _storageTypeCache = null)
    {
        private const string defaultPublicBaseUrl = "http://localhost";
        private const string defaultTimezone = "UTC";
        private const int defaultSessionTimeoutHours = 24 * 30;
        private const int defaultTotpMaxFailedAttempts = 64;
        private const int defaultEncryptionThreads = 2;
        private const int defaultMaxChunkSizeBytes = 4 * 1024 * 1024;
        private const int defaultCipherChunkSizeBytes = 1 * 1024 * 1024;
        private const int defaultCompressionLevel = CompressionProcessor.DefaultCompressionLevel;

        internal ServerSettingsSnapshot GetServerSettings()
        {
            return _cache.GetOrAdd(() =>
            {
                CottonServerSettings? settings;
                try
                {
                    settings = _dbContext.ServerSettings
                        .AsNoTracking()
                        .OrderByDescending(s => s.CreatedAt)
                        .FirstOrDefault();
                }
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
                {
                    settings = null;
                }
                if (settings is not null)
                {
                    return ServerSettingsSnapshot.FromEntity(settings);
                }

                CottonServerSettings defaults = CreateDefaultSettings(Guid.Empty, fallbackPublicBaseUrl: null);
                return ServerSettingsSnapshot.FromEntity(defaults);
            });
        }

        public async Task<string> GetPublicBaseUrlAsync(CancellationToken cancellationToken = default)
        {
            CottonServerSettings? settings = await LoadLatestSettingsAsync(asNoTracking: false, cancellationToken);
            if (settings is null)
            {
                return defaultPublicBaseUrl;
            }

            return settings.PublicBaseUrl.TrimEnd('/');
        }

        public async Task<CottonServerSettings> EnsureServerSettingsAsync(
            string? fallbackPublicBaseUrl,
            CancellationToken cancellationToken = default)
        {
            CottonServerSettings? settings = await LoadLatestSettingsAsync(asNoTracking: false, cancellationToken);
            if (settings is not null)
            {
                return settings;
            }

            return await _cache.RunCreationExclusiveAsync(async () =>
            {
                settings = await LoadLatestSettingsAsync(asNoTracking: false, cancellationToken);
                if (settings is not null)
                {
                    CacheRuntimePipelineSettings(settings);
                    return settings;
                }

                settings = CreateDefaultSettings(Guid.NewGuid(), fallbackPublicBaseUrl);
                await _dbContext.ServerSettings.AddAsync(settings, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                CacheRuntimePipelineSettings(settings);
                InvalidateSettingsCache(serverIsInitialized: true);
                return settings;
            }, cancellationToken);
        }

        public async Task<bool> IsServerInitializedAsync()
        {
            if (_cache.TryGetServerInitialized(out bool cached))
            {
                return cached;
            }

            bool value;
            try
            {
                value = await _dbContext.ServerSettings.AsNoTracking().AnyAsync();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                value = false;
            }

            _cache.SetServerInitialized(value);
            return value;
        }

        public async Task<bool> ServerHasUsersAsync()
        {
            if (_cache.TryGetServerHasUsers(out bool cached))
            {
                return cached;
            }

            bool value;
            try
            {
                value = await _dbContext.Users.AsNoTracking().AnyAsync();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                value = false;
            }

            _cache.SetServerHasUsers(value);
            return value;
        }

        public async Task ClearDefaultUserTemplateForOwnerAsync(
            Guid ownerId,
            CancellationToken cancellationToken = default)
        {
            CottonServerSettings? settings = await LoadLatestSettingsAsync(
                asNoTracking: false,
                cancellationToken);
            if (settings?.DefaultUserTemplateNodeId is not Guid templateNodeId)
            {
                return;
            }

            bool isOwnedByUser = await _dbContext.Nodes
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == templateNodeId && x.OwnerId == ownerId,
                    cancellationToken);
            if (!isOwnedByUser)
            {
                return;
            }

            settings.DefaultUserTemplateNodeId = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
            CacheRuntimePipelineSettings(settings);
            InvalidateSettingsCache(serverIsInitialized: true);
        }

        public async Task UpdateSettingsAsync(
            Action<CottonServerSettings> update,
            string? fallbackPublicBaseUrl,
            CancellationToken cancellationToken = default)
        {
            CottonServerSettings settings = await EnsureServerSettingsAsync(fallbackPublicBaseUrl, cancellationToken);
            update(settings);
            await _dbContext.SaveChangesAsync(cancellationToken);
            CacheRuntimePipelineSettings(settings);
            InvalidateSettingsCache(serverIsInitialized: true);
        }

        public async Task SetPropertyAsync<TProperty>(Expression<Func<CottonServerSettings, TProperty>> selector, TProperty value, CancellationToken cancellationToken = default)
        {
            await SetPropertyAsync(selector, value, fallbackPublicBaseUrl: null, cancellationToken);
        }

        public async Task SetPropertyAsync<TProperty>(
            Expression<Func<CottonServerSettings, TProperty>> selector,
            TProperty value,
            string? fallbackPublicBaseUrl,
            CancellationToken cancellationToken = default)
        {
            var memberExpression = selector.Body as MemberExpression;
            if (memberExpression is null && selector.Body is UnaryExpression unaryExpression)
            {
                memberExpression = unaryExpression.Operand as MemberExpression;
            }

            if (memberExpression?.Member.Name is not string propertyName)
            {
                throw new ArgumentException("Selector must point to a settings property.", nameof(selector));
            }

            CottonServerSettings settings = await EnsureServerSettingsAsync(fallbackPublicBaseUrl, cancellationToken);

            _dbContext.Entry(settings).Property(propertyName).CurrentValue = value;
            await _dbContext.SaveChangesAsync(cancellationToken);
            CacheRuntimePipelineSettings(settings);
            InvalidateSettingsCache(serverIsInitialized: true);
        }

        public static string NormalizePublicBaseUrl(string? url)
        {
            return TryNormalizePublicBaseUrl(url, out string? normalized)
                ? normalized
                : defaultPublicBaseUrl;
        }

        internal static bool TryNormalizePublicBaseUrl(string? url, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            string trimmed = url.Trim().TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
            {
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            normalized = trimmed;
            return true;
        }

        private async Task<CottonServerSettings?> LoadLatestSettingsAsync(
            bool asNoTracking,
            CancellationToken cancellationToken)
        {
            IQueryable<CottonServerSettings> query = _dbContext.ServerSettings
                .OrderByDescending(s => s.CreatedAt);

            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            try
            {
                CottonServerSettings? settings = await query.FirstOrDefaultAsync(cancellationToken);
                return settings;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                return null;
            }
        }

        private static CottonServerSettings CreateDefaultSettings(
            Guid instanceId,
            string? fallbackPublicBaseUrl)
        {
            return new()
            {
                AllowCrossUserDeduplication = false,
                AllowGlobalIndexing = false,
                CipherChunkSizeBytes = defaultCipherChunkSizeBytes,
                CompressionLevel = defaultCompressionLevel,
                EncryptionThreads = defaultEncryptionThreads,
                MaxChunkSizeBytes = defaultMaxChunkSizeBytes,
                SessionTimeoutHours = defaultSessionTimeoutHours,
                TelemetryEnabled = false,
                DisableVersionCheck = false,
                Timezone = defaultTimezone,
                TotpMaxFailedAttempts = defaultTotpMaxFailedAttempts,
                EmailMode = EmailMode.None,
                ComputionMode = ComputionMode.Local,
                StorageType = StorageType.Local,
                InstanceId = instanceId,
                PublicBaseUrl = NormalizePublicBaseUrl(fallbackPublicBaseUrl),
                ServerUsage = [ServerUsage.Other],
                StorageSpaceMode = StorageSpaceMode.Optimal,
                DefaultUserStorageQuotaBytes = null,
                DefaultUserTemplateNodeId = null,
                GeoIpLookupMode = GeoIpLookupMode.Disabled,
            };
        }

        private void InvalidateSettingsCache(bool serverIsInitialized)
        {
            _cache.InvalidateSettings(serverIsInitialized);

            // Reset after the settings cache is cleared: a backend-type fill racing with this
            // invalidation then either resolves fresh settings or gets wiped by the reset below.
            _storageTypeCache?.Reset();
        }

        private void CacheRuntimePipelineSettings(CottonServerSettings settings)
        {
            _cache.CacheEncryptionThreads(settings.EncryptionThreads);
        }
    }
}
