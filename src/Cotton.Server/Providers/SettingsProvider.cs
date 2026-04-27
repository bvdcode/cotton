// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Amazon.S3;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Linq.Expressions;

namespace Cotton.Server.Providers
{
    public class SettingsProvider(
        CottonDbContext _dbContext)
    {
        private static readonly Lock _cacheLock = new();
        private static CottonServerSettings? _cache;
        private static readonly TimeSpan _boolCacheTtl = TimeSpan.FromMinutes(1);
        private static (bool Value, DateTimeOffset CachedAt)? _isServerInitializedCache;
        private static (bool Value, DateTimeOffset CachedAt)? _serverHasUsersCache;
        private const int defaultSessionTimeoutHours = 24 * 30;
        private const int defaultTotpMaxFailedAttempts = 64;
        private const int defaultEncryptionThreads = 2;
        private const int defaultMaxChunkSizeBytes = 4 * 1024 * 1024;
        private const int defaultCipherChunkSizeBytes = 1 * 1024 * 1024;

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
                    _cache = settings;
                    // TODO: Set Timezone somewhere more appropriate, and handle timezone changes
                    Environment.SetEnvironmentVariable("TZ", settings.Timezone);
                    return _cache;
                }

                _cache = new()
                {
                    AllowCrossUserDeduplication = false,
                    AllowGlobalIndexing = false,
                    CipherChunkSizeBytes = defaultCipherChunkSizeBytes,
                    EncryptionThreads = defaultEncryptionThreads,
                    MaxChunkSizeBytes = defaultMaxChunkSizeBytes,
                    SessionTimeoutHours = defaultSessionTimeoutHours,
                    TelemetryEnabled = false,
                    Timezone = "America/Los_Angeles",
                    TotpMaxFailedAttempts = defaultTotpMaxFailedAttempts,
                };
                return _cache;
            }
        }

        public async Task<bool> IsServerInitializedAsync()
        {
            var now = DateTimeOffset.UtcNow;
            lock (_cacheLock)
            {
                if (_isServerInitializedCache is { } cached && now - cached.CachedAt < _boolCacheTtl)
                {
                    return cached.Value;
                }
            }

            bool value = await _dbContext.ServerSettings.AsNoTracking().AnyAsync();
            lock (_cacheLock)
            {
                _isServerInitializedCache = (value, DateTimeOffset.UtcNow);
            }
            return value;
        }

        public async Task<bool> ServerHasUsersAsync()
        {
            var now = DateTimeOffset.UtcNow;
            lock (_cacheLock)
            {
                if (_serverHasUsersCache is { } cached && now - cached.CachedAt < _boolCacheTtl)
                {
                    return cached.Value;
                }
            }

            bool value = await _dbContext.Users.AsNoTracking().AnyAsync();
            lock (_cacheLock)
            {
                _serverHasUsersCache = (value, DateTimeOffset.UtcNow);
            }
            return value;
        }

        public async Task<string?> ValidateServerSettingsAsync(CottonServerSettingsDto request)
        {
            if (!IsTimezoneValid(request.Timezone))
            {
                return "Timezone not found: " + request.Timezone;
            }

            var telemetryError = ValidateTelemetryConstraints(request);
            if (telemetryError is not null)
            {
                return telemetryError;
            }

            var emailError = await ValidateEmailConstraintsAsync(request);
            if (emailError is not null)
            {
                return emailError;
            }

            var storageError = await ValidateStorageConstraintsAsync(request);
            if (storageError is not null)
            {
                return storageError;
            }

            return null;
        }

        private static bool IsTimezoneValid(string timezone)
        {
            return TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out _);
        }

        private static string? ValidateTelemetryConstraints(CottonServerSettingsDto request)
        {
            if (request.Telemetry)
            {
                return null;
            }

            if (request.Email == EmailMode.Cloud)
            {
                return "Telemetry must be enabled to use cloud email service.";
            }

            if (request.ComputionMode == ComputionMode.Cloud)
            {
                return "Telemetry must be enabled to use cloud AI service.";
            }

            return null;
        }

        private static async Task<string?> ValidateEmailConstraintsAsync(CottonServerSettingsDto request)
        {
            if (request.Email == EmailMode.Cloud)
            {
                if (!request.Telemetry)
                {
                    return "Telemetry must be enabled to use cloud email service.";
                }

                bool isHealthy = await CheckGatewayHealthAsync();
                if (!isHealthy)
                {
                    return "Cloud email service is currently unavailable. Please try again later or switch to Custom email service.";
                }

                return null;
            }

            if (request.Email == EmailMode.Custom)
            {
                return request.EmailConfig is null
                    ? "EmailConfig must be provided when using Custom email service."
                    : null;
            }

            if (request.Email == EmailMode.None)
            {
                return null;
            }

            return "Invalid email mode: " + request.Email;
        }

        private static async Task<bool> CheckGatewayHealthAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var response = await client.GetFromJsonAsync<HealthResponse>(CottonPublicEmailProvider.GatewayBaseUrl + "health");
                return response != null && response.Status == "Healthy";
            }
            catch
            {
                return false;
            }
        }

        private static async Task<string?> ValidateStorageConstraintsAsync(CottonServerSettingsDto request)
        {
            if (request.Storage != StorageType.S3)
            {
                return null;
            }

            if (request.S3Config is null)
            {
                return "S3Config must be provided when using S3 storage.";
            }

            try
            {
                await ValidateS3Async(request.S3Config);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static async Task ValidateS3Async(S3Config s3Config)
        {
            var config = new AmazonS3Config
            {
                UseHttp = false,
                MaxErrorRetry = 5,
                ForcePathStyle = true,
                ServiceURL = s3Config.Endpoint,
                AuthenticationRegion = s3Config.Region,
                RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED
            };
            var s3 = new AmazonS3Client(s3Config.AccessKey, s3Config.SecretKey, config);

            // try write access by creating and deleting a test object
            string testKey = "cotton_server_test_object_" + Guid.NewGuid().ToString("N");
            await s3.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
            {
                BucketName = s3Config.Bucket,
                Key = testKey,
                ContentBody = "test"
            });

            // try read access by getting the test object
            var getResponse = await s3.GetObjectAsync(s3Config.Bucket, testKey);
            using (var reader = new StreamReader(getResponse.ResponseStream))
            {
                string content = await reader.ReadToEndAsync();
                if (content != "test")
                {
                    throw new Exception("S3 read access validation failed: content mismatch.");
                }
            }

            // try list all objects in the bucket
            var listResponse = await s3.ListObjectsV2Async(new Amazon.S3.Model.ListObjectsV2Request
            {
                BucketName = s3Config.Bucket,
                MaxKeys = 1
            });
            if (listResponse.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception("S3 list access validation failed: " + listResponse.HttpStatusCode);
            }
            if (listResponse.KeyCount <= 0)
            {
                throw new Exception("S3 list access validation failed: bucket is empty or inaccessible.");
            }

            // clean up the test object
            await s3.DeleteObjectAsync(s3Config.Bucket, testKey);
        }

        public async Task SaveServerSettingsAsync(CottonServerSettingsDto request)
        {
            int? smtpPort = TryParseInt(request.EmailConfig?.Port);
            var lastSettings = await _dbContext.ServerSettings
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
            Guid instanceId = lastSettings?.InstanceId ?? Guid.NewGuid();
            CottonServerSettings newSettings = new()
            {
                EmailMode = request.Email,
                ComputionMode = request.ComputionMode,
                StorageType = request.Storage,
                EncryptionThreads = defaultEncryptionThreads,
                MaxChunkSizeBytes = defaultMaxChunkSizeBytes,
                CipherChunkSizeBytes = defaultCipherChunkSizeBytes,
                SessionTimeoutHours = defaultSessionTimeoutHours,
                AllowCrossUserDeduplication = request.TrustedMode,
                AllowGlobalIndexing = request.TrustedMode,
                TelemetryEnabled = request.Telemetry,
                Timezone = request.Timezone,
                SmtpServerAddress = request.EmailConfig?.SmtpServer,
                SmtpServerPort = smtpPort,
                SmtpUsername = request.EmailConfig?.Username,
                SmtpPasswordEncrypted = request.EmailConfig?.Password,
                SmtpSenderEmail = request.EmailConfig?.FromAddress,
                SmtpUseSsl = request.EmailConfig?.UseSSL ?? false,
                S3AccessKeyId = request.S3Config?.AccessKey,
                S3SecretAccessKeyEncrypted = request.S3Config?.SecretKey,
                S3BucketName = request.S3Config?.Bucket,
                S3Region = request.S3Config?.Region,
                S3EndpointUrl = request.S3Config?.Endpoint,
                InstanceId = instanceId,
                // TODO: Double check if public base url is not null
                PublicBaseUrl = request.PublicBaseUrl!.TrimEnd('/'),
                ServerUsage = request.Usage,
                StorageSpaceMode = request.StorageSpace,
                TotpMaxFailedAttempts = defaultTotpMaxFailedAttempts,
            };
            await _dbContext.ServerSettings.AddAsync(newSettings);
            await _dbContext.SaveChangesAsync();
            _cache = null;
            lock (_cacheLock)
            {
                _isServerInitializedCache = (true, DateTimeOffset.UtcNow);
            }
        }

        public async Task SetPropertyAsync<TProperty>(Expression<Func<CottonServerSettings, TProperty>> selector, TProperty value, CancellationToken cancellationToken = default)
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

            CottonServerSettings? settings = await _dbContext.ServerSettings
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Server settings are not initialized.");

            _dbContext.Entry(settings).Property(propertyName).CurrentValue = value;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _cache = null;
        }

        private static int? TryParseInt(string? value)
        {
            return int.TryParse(value, out int i) ? i : null;
        }
    }
}
