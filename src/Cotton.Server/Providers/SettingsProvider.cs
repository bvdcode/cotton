// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Amazon.S3;
using Amazon.S3.Model;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Storage.Helpers;
using Cotton.Storage.Processors;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Linq.Expressions;
using System.Net.Mail;
using System.Text.Json;

namespace Cotton.Server.Providers
{
    /// <summary>
    /// Provides settings dependencies to server components.
    /// </summary>
    public class SettingsProvider(
        CottonDbContext _dbContext,
        IStorageBackendTypeCache? _storageTypeCache = null,
        IDatabaseIntegrityVerifier? _integrity = null)
    {
        private static readonly Lock _cacheLock = new();
        private static readonly SemaphoreSlim _settingsCreationLock = new(1, 1);
        private static CottonServerSettings? _cache;
        private static int _cachedEncryptionThreads;
        private static readonly TimeSpan _boolCacheTtl = TimeSpan.FromMinutes(1);
        private static (bool Value, DateTimeOffset CachedAt)? _isServerInitializedCache;
        private static (bool Value, DateTimeOffset CachedAt)? _serverHasUsersCache;
        private const string defaultPublicBaseUrl = "http://localhost";
        private const string defaultTimezone = "UTC";
        private const int defaultSessionTimeoutHours = 24 * 30;
        private const int defaultTotpMaxFailedAttempts = 64;
        private const int defaultEncryptionThreads = 2;
        private const int defaultMaxChunkSizeBytes = 4 * 1024 * 1024;
        private const int defaultCipherChunkSizeBytes = 1 * 1024 * 1024;
        private const int defaultCompressionLevel = CompressionProcessor.DefaultCompressionLevel;

        /// <summary>
        /// Gets server settings.
        /// </summary>
        public CottonServerSettings GetServerSettings()
        {
            if (_cache is not null)
            {
                return _cache;
            }

            lock (_cacheLock)
            {
                if (_cache is not null)
                {
                    return _cache;
                }

                CottonServerSettings? settings;
                try
                {
                    settings = _dbContext.ServerSettings
                        .OrderByDescending(s => s.CreatedAt)
                        .FirstOrDefault();
                }
                catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
                {
                    settings = null;
                }
                if (settings is not null)
                {
                    _integrity?.RequireValid(_dbContext, settings, "settings.cache-load");
                    CacheRuntimePipelineSettings(settings);
                    _cache = settings;
                    return _cache;
                }

                _cache = new()
                {
                    AllowCrossUserDeduplication = false,
                    AllowGlobalIndexing = false,
                    CipherChunkSizeBytes = defaultCipherChunkSizeBytes,
                    CompressionLevel = defaultCompressionLevel,
                    EncryptionThreads = defaultEncryptionThreads,
                    MaxChunkSizeBytes = defaultMaxChunkSizeBytes,
                    SessionTimeoutHours = defaultSessionTimeoutHours,
                    TelemetryEnabled = false,
                    Timezone = defaultTimezone,
                    TotpMaxFailedAttempts = defaultTotpMaxFailedAttempts,
                    EmailMode = EmailMode.None,
                    ComputionMode = ComputionMode.Local,
                    StorageType = StorageType.Local,
                    InstanceId = Guid.Empty,
                    PublicBaseUrl = defaultPublicBaseUrl,
                    ServerUsage = [ServerUsage.Other],
                    StorageSpaceMode = StorageSpaceMode.Optimal,
                    DefaultUserStorageQuotaBytes = null,
                    DefaultUserTemplateNodeId = null,
                    GeoIpLookupMode = GeoIpLookupMode.Disabled,
                };
                CacheRuntimePipelineSettings(_cache);
                return _cache;
            }
        }

        internal static int? GetCachedEncryptionThreads()
        {
            int value = Volatile.Read(ref _cachedEncryptionThreads);
            return value > 0 ? value : null;
        }

        /// <summary>
        /// Gets the canonical public base URL from persisted server settings.
        /// </summary>
        public async Task<string> GetPublicBaseUrlAsync(CancellationToken cancellationToken = default)
        {
            CottonServerSettings? settings = await LoadLatestSettingsAsync(asNoTracking: false, cancellationToken);
            if (settings is null)
            {
                return defaultPublicBaseUrl;
            }

            return settings.PublicBaseUrl.TrimEnd('/');
        }

        /// <summary>
        /// Ensures server settings async.
        /// </summary>
        public async Task<CottonServerSettings> EnsureServerSettingsAsync(
            string? fallbackPublicBaseUrl,
            CancellationToken cancellationToken = default)
        {
            CottonServerSettings? settings = await LoadLatestSettingsAsync(asNoTracking: false, cancellationToken);
            if (settings is not null)
            {
                return settings;
            }

            await _settingsCreationLock.WaitAsync(cancellationToken);
            try
            {
                settings = await LoadLatestSettingsAsync(asNoTracking: false, cancellationToken);
                if (settings is not null)
                {
                    CacheRuntimePipelineSettings(settings);
                    return settings;
                }

                settings = CreateDefaultSettings(fallbackPublicBaseUrl);
                await _dbContext.ServerSettings.AddAsync(settings, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                CacheRuntimePipelineSettings(settings);
                InvalidateSettingsCache(serverIsInitialized: true);
                return settings;
            }
            finally
            {
                _settingsCreationLock.Release();
            }
        }

        /// <summary>
        /// Indicates whether server initialized async.
        /// </summary>
        public async Task<bool> IsServerInitializedAsync()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            lock (_cacheLock)
            {
                if (_isServerInitializedCache is { } cached && now - cached.CachedAt < _boolCacheTtl)
                {
                    return cached.Value;
                }
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

            lock (_cacheLock)
            {
                _isServerInitializedCache = (value, DateTimeOffset.UtcNow);
            }
            return value;
        }

        /// <summary>
        /// Checks whether any user account exists.
        /// </summary>
        public async Task<bool> ServerHasUsersAsync()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            lock (_cacheLock)
            {
                if (_serverHasUsersCache is { } cached && now - cached.CachedAt < _boolCacheTtl)
                {
                    return cached.Value;
                }
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

            lock (_cacheLock)
            {
                _serverHasUsersCache = (value, DateTimeOffset.UtcNow);
            }
            return value;
        }

        /// <summary>
        /// Validates timezone.
        /// </summary>
        public string? ValidateTimezone(string? timezone)
        {
            if (string.IsNullOrWhiteSpace(timezone))
            {
                return "Timezone must be provided.";
            }

            if (!IsTimezoneValid(timezone))
            {
                return "Timezone not found: " + timezone;
            }

            return null;
        }

        /// <summary>
        /// Validates telemetry change.
        /// </summary>
        public string? ValidateTelemetryChange(bool enabled)
        {
            if (enabled)
            {
                return null;
            }

            CottonServerSettings settings = GetServerSettings();

            if (settings.EmailMode == EmailMode.Cloud)
            {
                return "Telemetry must be enabled to use Cotton Bridge Mail.";
            }

            if (settings.ComputionMode == ComputionMode.Cloud)
            {
                return "Telemetry must be enabled to use Cotton Bridge AI.";
            }

            if (settings.GeoIpLookupMode == GeoIpLookupMode.CottonCloud)
            {
                return "Telemetry must be enabled to use Cotton Bridge IP lookup.";
            }

            return null;
        }

        /// <summary>
        /// Validates email mode async.
        /// </summary>
        public async Task<string?> ValidateEmailModeAsync(EmailMode mode)
        {
            CottonServerSettings settings = GetServerSettings();

            if (mode == EmailMode.Cloud)
            {
                if (!settings.TelemetryEnabled)
                {
                    return "Telemetry must be enabled to use Cotton Bridge Mail.";
                }

                bool isHealthy = await CheckCottonBridgeHealthAsync();
                if (!isHealthy)
                {
                    return "Cotton Bridge Mail is currently unavailable. Please try again later or switch to Custom email service.";
                }

                return null;
            }

            if (mode == EmailMode.Custom)
            {
                return IsEmailConfigComplete(settings)
                    ? null
                    : "SMTP settings must be configured before enabling Custom email service.";
            }

            if (mode == EmailMode.None)
            {
                return null;
            }

            return "Invalid email mode: " + mode;
        }

        /// <summary>
        /// Validates compution mode.
        /// </summary>
        public string? ValidateComputionMode(ComputionMode mode)
        {
            if (mode == ComputionMode.Cloud && !GetServerSettings().TelemetryEnabled)
            {
                return "Telemetry must be enabled to use Cotton Bridge AI.";
            }

            return Enum.IsDefined(mode)
                ? null
                : "Invalid computation mode: " + mode;
        }

        /// <summary>
        /// Validates geo ip lookup mode.
        /// </summary>
        public string? ValidateGeoIpLookupMode(GeoIpLookupMode mode)
        {
            CottonServerSettings settings = GetServerSettings();

            if (mode == GeoIpLookupMode.CottonCloud && !settings.TelemetryEnabled)
            {
                return "Telemetry must be enabled to use Cotton Bridge IP lookup.";
            }

            if (mode == GeoIpLookupMode.CustomHttp && string.IsNullOrWhiteSpace(settings.CustomGeoIpLookupUrl))
            {
                return "Custom GeoIP lookup URL must be configured before enabling Custom HTTP lookup.";
            }

            if (mode == GeoIpLookupMode.MaxMindLocal)
            {
                return "MaxMind local lookup is not configurable yet.";
            }

            return Enum.IsDefined(mode)
                ? null
                : "Invalid GeoIP lookup mode: " + mode;
        }

        private static async Task<bool> CheckCottonBridgeHealthAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                HealthResponse? response = await client.GetFromJsonAsync<HealthResponse>(global::Cotton.Constants.CottonBridgeHealthUrl);
                return response is not null && response.Status == "Healthy";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates storage type async.
        /// </summary>
        public async Task<string?> ValidateStorageTypeAsync(StorageType type)
        {
            if (type == StorageType.Local)
            {
                return null;
            }

            if (type != StorageType.S3)
            {
                return "Invalid storage type: " + type;
            }

            CottonServerSettings settings = GetServerSettings();
            var s3Config = new S3Config
            {
                AccessKey = settings.S3AccessKeyId ?? string.Empty,
                SecretKey = settings.S3SecretAccessKeyEncrypted ?? string.Empty,
                Endpoint = settings.S3EndpointUrl ?? string.Empty,
                Region = settings.S3Region ?? string.Empty,
                Bucket = settings.S3BucketName ?? string.Empty
            };

            var configError = ValidateS3ConfigShape(s3Config);
            if (configError is not null)
            {
                return "S3 settings must be configured before enabling S3 storage.";
            }

            return await ValidateS3ConnectivityAsync(s3Config);
        }

        /// <summary>
        /// Validates s3 config async.
        /// </summary>
        public async Task<string?> ValidateS3ConfigAsync(S3Config? s3Config)
        {
            var shapeError = ValidateS3ConfigShape(s3Config);
            if (shapeError is not null)
            {
                return shapeError;
            }

            return await ValidateS3ConnectivityAsync(s3Config!);
        }

        private static string? ValidateS3ConfigShape(S3Config? s3Config)
        {
            if (s3Config is null)
            {
                return "S3 settings must be provided.";
            }

            if (string.IsNullOrWhiteSpace(s3Config.Endpoint))
            {
                return "S3 endpoint URL must be provided.";
            }

            if (!Uri.TryCreate(s3Config.Endpoint, UriKind.Absolute, out Uri? endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
            {
                return "S3 endpoint URL must be an absolute HTTP or HTTPS URL.";
            }

            if (string.IsNullOrWhiteSpace(s3Config.Region))
            {
                return "S3 region must be provided.";
            }

            if (string.IsNullOrWhiteSpace(s3Config.Bucket))
            {
                return "S3 bucket must be provided.";
            }

            if (string.IsNullOrWhiteSpace(s3Config.AccessKey))
            {
                return "S3 access key must be provided.";
            }

            if (string.IsNullOrWhiteSpace(s3Config.SecretKey))
            {
                return "S3 secret key must be provided.";
            }

            return null;
        }

        private static async Task<string?> ValidateS3ConnectivityAsync(S3Config s3Config)
        {
            try
            {
                await ValidateS3Async(s3Config);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static async Task ValidateS3Async(S3Config s3Config)
        {
            AmazonS3Client s3 = S3CompatibilityFactory.BuildClient(
                s3Config.Endpoint,
                s3Config.Region,
                s3Config.AccessKey,
                s3Config.SecretKey,
                timeout: TimeSpan.FromSeconds(30),
                maxErrorRetry: 5);

            // try write access by creating and deleting a test object
            string testKey = "cotton_server_test_object_" + Guid.NewGuid().ToString("N");
            await s3.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
            {
                BucketName = s3Config.Bucket,
                Key = testKey,
                ContentBody = "test"
            }.WithInMemoryBodyCompatibility());

            // try read access by getting the test object
            GetObjectResponse getResponse = await s3.GetObjectAsync(s3Config.Bucket, testKey);
            using (var reader = new StreamReader(getResponse.ResponseStream))
            {
                string content = await reader.ReadToEndAsync();
                if (content != "test")
                {
                    throw new InvalidOperationException("S3 read access validation failed: content mismatch.");
                }
            }

            // try list all objects in the bucket
            ListObjectsV2Response listResponse = await s3.ListObjectsV2Async(new Amazon.S3.Model.ListObjectsV2Request
            {
                BucketName = s3Config.Bucket,
                MaxKeys = 1
            });
            if (listResponse.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new InvalidOperationException("S3 list access validation failed: " + listResponse.HttpStatusCode);
            }
            if (listResponse.KeyCount <= 0)
            {
                throw new InvalidOperationException("S3 list access validation failed: bucket is empty or inaccessible.");
            }

            // clean up the test object
            await s3.DeleteObjectAsync(s3Config.Bucket, testKey);
        }

        /// <summary>
        /// Validates email config.
        /// </summary>
        public string? ValidateEmailConfig(EmailConfig? emailConfig)
        {
            if (emailConfig is null)
            {
                return "SMTP settings must be provided.";
            }

            if (string.IsNullOrWhiteSpace(emailConfig.SmtpServer))
            {
                return "SMTP server must be provided.";
            }

            if (!TryParsePort(emailConfig.Port, out _))
            {
                return "SMTP port must be a number between 1 and 65535.";
            }

            if (string.IsNullOrWhiteSpace(emailConfig.Username))
            {
                return "SMTP username must be provided.";
            }

            if (string.IsNullOrWhiteSpace(emailConfig.Password))
            {
                return "SMTP password must be provided.";
            }

            if (string.IsNullOrWhiteSpace(emailConfig.FromAddress))
            {
                return "SMTP sender address must be provided.";
            }

            try
            {
                _ = new MailAddress(emailConfig.FromAddress);
            }
            catch (FormatException)
            {
                return "SMTP sender address must be a valid email address.";
            }

            return null;
        }

        /// <summary>
        /// Validates Firebase Cloud Messaging config.
        /// </summary>
        public string? ValidateFirebaseCloudMessagingConfig(FirebaseCloudMessagingConfig? config)
        {
            if (config is null)
            {
                return "Firebase Cloud Messaging settings must be provided.";
            }

            if (string.IsNullOrWhiteSpace(config.ProjectId))
            {
                return "Firebase Cloud Messaging project ID must be provided.";
            }

            if (string.IsNullOrWhiteSpace(config.ServiceAccountJson))
            {
                return "Firebase Cloud Messaging service account JSON must be provided.";
            }

            return ValidateFirebaseCloudMessagingServiceAccountJson(config.ServiceAccountJson);
        }

        /// <summary>
        /// Validates default user storage quota bytes.
        /// </summary>
        public string? ValidateDefaultUserStorageQuotaBytes(long? quotaBytes)
        {
            if (quotaBytes is null or 0)
            {
                return null;
            }

            return quotaBytes > 0
                ? null
                : "Default user storage quota must be zero, empty, or a positive byte value.";
        }

        /// <summary>
        /// Validates default user template node id async.
        /// </summary>
        public async Task<string?> ValidateDefaultUserTemplateNodeIdAsync(
            Guid? nodeId,
            Guid ownerId,
            CancellationToken cancellationToken = default)
        {
            if (nodeId is null || nodeId == Guid.Empty)
            {
                return null;
            }

            bool exists = await _dbContext.Nodes
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == nodeId.Value
                    && x.OwnerId == ownerId
                    && x.Type == NodeType.Default,
                    cancellationToken);

            return exists
                ? null
                : "Default user template folder was not found.";
        }

        /// <summary>
        /// Validates public base url.
        /// </summary>
        public string? ValidatePublicBaseUrl(string? url)
        {
            return TryNormalizePublicBaseUrl(url, out _)
                ? null
                : "Public base URL must be an absolute HTTP or HTTPS URL.";
        }

        /// <summary>
        /// Validates custom geo ip lookup url.
        /// </summary>
        public string? ValidateCustomGeoIpLookupUrl(string? url)
        {
            return TryNormalizePublicBaseUrl(url, out _)
                ? null
                : "Custom GeoIP lookup URL must be an absolute HTTP or HTTPS URL.";
        }

        /// <summary>
        /// Updates settings async.
        /// </summary>
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

        /// <summary>
        /// Updates a single server setting property.
        /// </summary>
        public async Task SetPropertyAsync<TProperty>(Expression<Func<CottonServerSettings, TProperty>> selector, TProperty value, CancellationToken cancellationToken = default)
        {
            await SetPropertyAsync(selector, value, fallbackPublicBaseUrl: null, cancellationToken);
        }

        /// <summary>
        /// Updates a single server setting property.
        /// </summary>
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

        /// <summary>
        /// Normalizes the public base URL for storage and comparison.
        /// </summary>
        public static string NormalizePublicBaseUrl(string? url)
        {
            return TryNormalizePublicBaseUrl(url, out string? normalized)
                ? normalized
                : defaultPublicBaseUrl;
        }

        /// <summary>
        /// Attempts to parse port.
        /// </summary>
        public static bool TryParsePort(string? value, out int port)
        {
            return int.TryParse(value, out port) && port is >= 1 and <= 65535;
        }
        private static bool IsTimezoneValid(string timezone)
        {
            return TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out _);
        }

        private static bool TryNormalizePublicBaseUrl(string? url, out string normalized)
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

        private static string? ValidateFirebaseCloudMessagingServiceAccountJson(string serviceAccountJson)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(serviceAccountJson);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return "Firebase Cloud Messaging service account JSON must be a JSON object.";
                }

                if (!HasRequiredJsonString(root, "client_email"))
                {
                    return "Firebase Cloud Messaging service account JSON must include client_email.";
                }

                if (!HasRequiredJsonString(root, "private_key"))
                {
                    return "Firebase Cloud Messaging service account JSON must include private_key.";
                }

                if (root.TryGetProperty("type", out JsonElement type)
                    && type.ValueKind == JsonValueKind.String
                    && !string.Equals(type.GetString(), "service_account", StringComparison.Ordinal))
                {
                    return "Firebase Cloud Messaging service account JSON must describe a service_account.";
                }

                return null;
            }
            catch (JsonException)
            {
                return "Firebase Cloud Messaging service account JSON must be valid JSON.";
            }
        }

        private static bool HasRequiredJsonString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString());
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
                if (settings is not null && !asNoTracking)
                {
                    _integrity?.RequireValid(_dbContext, settings, "settings.load");
                }

                return settings;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                return null;
            }
        }

        private static CottonServerSettings CreateDefaultSettings(string? fallbackPublicBaseUrl)
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
                Timezone = defaultTimezone,
                TotpMaxFailedAttempts = defaultTotpMaxFailedAttempts,
                EmailMode = EmailMode.None,
                ComputionMode = ComputionMode.Local,
                StorageType = StorageType.Local,
                InstanceId = Guid.NewGuid(),
                PublicBaseUrl = NormalizePublicBaseUrl(fallbackPublicBaseUrl),
                ServerUsage = [ServerUsage.Other],
                StorageSpaceMode = StorageSpaceMode.Optimal,
                DefaultUserStorageQuotaBytes = null,
                DefaultUserTemplateNodeId = null,
                GeoIpLookupMode = GeoIpLookupMode.Disabled,
            };
        }

        private static bool IsEmailConfigComplete(CottonServerSettings settings)
        {
            return !string.IsNullOrWhiteSpace(settings.SmtpServerAddress)
                && settings.SmtpServerPort is >= 1 and <= 65535
                && !string.IsNullOrWhiteSpace(settings.SmtpUsername)
                && !string.IsNullOrWhiteSpace(settings.SmtpPasswordEncrypted)
                && !string.IsNullOrWhiteSpace(settings.SmtpSenderEmail);
        }

        private void InvalidateSettingsCache(bool serverIsInitialized)
        {
            _storageTypeCache?.Reset();

            lock (_cacheLock)
            {
                _cache = null;
                if (serverIsInitialized)
                {
                    _isServerInitializedCache = (true, DateTimeOffset.UtcNow);
                }
            }
        }

        private static void CacheRuntimePipelineSettings(CottonServerSettings settings)
        {
            int encryptionThreads = settings.EncryptionThreads > 0 ? settings.EncryptionThreads : 0;
            Volatile.Write(ref _cachedEncryptionThreads, encryptionThreads);
        }
    }
}
