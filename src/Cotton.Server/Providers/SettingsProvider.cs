// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Amazon.S3;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using Cotton.Server.Services;
using EasyExtensions.Abstractions;
using EasyExtensions.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

namespace Cotton.Server.Providers
{
    public class SettingsProvider(
        IStreamCipher _crypto,
        CottonDbContext _dbContext)
    {
        private static readonly Lock _cacheLock = new();
        private static CottonServerSettings? _cache;
        private const int defaultSessionTimeoutHours = 24 * 30;
        private const int defaultTotpMaxFailedAttempts = 64;
        private const int defaultEncryptionThreads = 2;
        private const int defaultMaxChunkSizeBytes = 4 * 1024 * 1024;
        private const int defaultCipherChunkSizeBytes = 1 * 1024 * 1024;

        public string? DecryptValue(string? encryptedValue)
        {
            if (string.IsNullOrWhiteSpace(encryptedValue))
            {
                return null;
            }
            byte[] encryptedBytes = Convert.FromBase64String(encryptedValue);
            return _crypto.Decrypt(encryptedBytes);
        }

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

                var settings = _dbContext.ServerSettings
                    .AsNoTracking()
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefault();
                if (settings is not null)
                {
                    _cache = settings;
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

        public async Task<string?> EnsurePublicBaseUrlAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            var cached = GetServerSettings();
            if (!string.IsNullOrWhiteSpace(cached.PublicBaseUrl))
            {
                return cached.PublicBaseUrl;
            }

            string? incoming = TryBuildBaseUrl(request);
            if (string.IsNullOrWhiteSpace(incoming))
            {
                return null;
            }

            // Capture in cache first (covers pre-initialization requests).
            lock (_cacheLock)
            {
                _cache ??= cached;
                if (string.IsNullOrWhiteSpace(_cache.PublicBaseUrl))
                {
                    _cache.PublicBaseUrl = incoming;
                }
            }

            // Persist to DB if server settings already exist.
            var lastSettings = await _dbContext.ServerSettings
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastSettings is not null && string.IsNullOrWhiteSpace(lastSettings.PublicBaseUrl))
            {
                lastSettings.PublicBaseUrl = incoming;
                await _dbContext.SaveChangesAsync(cancellationToken);
                InvalidateCache();
            }

            return incoming;
        }

        private static string? TryBuildBaseUrl(HttpRequest request)
        {
            var host = request.Host;
            if (!host.HasValue)
            {
                return null;
            }

            string scheme = string.IsNullOrWhiteSpace(request.Scheme) ? "https" : request.Scheme;
            string candidate = $"{scheme}://{host.Value}";

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                return null;
            }

            // Avoid capturing obvious local dev/loopback addresses.
            if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (System.Net.IPAddress.TryParse(uri.Host, out var ip) && System.Net.IPAddress.IsLoopback(ip))
            {
                return null;
            }

            return uri.GetLeftPart(UriPartial.Authority);
        }

        public static void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cache = null;
            }
        }

        public Task<bool> IsServerInitializedAsync()
        {
            return _dbContext.ServerSettings.AnyAsync();
        }

        public Task<bool> ServerHasUsersAsync()
        {
            return _dbContext.Users.AnyAsync();
        }

        public async Task<string?> ValidateServerSettingsAsync(ServerSettingsRequestDto request)
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

            var importError = ValidateImportConstraints(request);
            if (importError is not null)
            {
                return importError;
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

        private static string? ValidateTelemetryConstraints(ServerSettingsRequestDto request)
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

        private static async Task<string?> ValidateEmailConstraintsAsync(ServerSettingsRequestDto request)
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
                var response = await client.GetFromJsonAsync<HealthResponse>(
                    "https://cotton-gateway.splidex.com/api/v1/health");
                return response != null && response.Status == "Healthy";
            }
            catch
            {
                return false;
            }
        }

        private static string? ValidateImportConstraints(ServerSettingsRequestDto request)
        {
            if (request.ImportSource != ImportSource.Webdav)
            {
                return null;
            }

            return request.WebdavConfig is null
                ? "WebdavConfig must be provided when using Webdav import source."
                : null;
        }

        private static async Task<string?> ValidateStorageConstraintsAsync(ServerSettingsRequestDto request)
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

        public async Task SaveServerSettingsAsync(ServerSettingsRequestDto request)
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
                ImportSource = request.ImportSource,
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
                SmtpPasswordEncrypted = TryEncrypt(request.EmailConfig?.Password),
                SmtpSenderEmail = request.EmailConfig?.FromAddress,
                SmtpUseSsl = request.EmailConfig?.UseSSL ?? false,
                S3AccessKeyId = request.S3Config?.AccessKey,
                S3SecretAccessKeyEncrypted = TryEncrypt(request.S3Config?.SecretKey),
                S3BucketName = request.S3Config?.Bucket,
                S3Region = request.S3Config?.Region,
                S3EndpointUrl = request.S3Config?.Endpoint,
                InstanceId = instanceId,
                PublicBaseUrl = lastSettings?.PublicBaseUrl ?? GetServerSettings().PublicBaseUrl,
                ServerUsage = request.Usage,
                StorageSpaceMode = request.StorageSpace,
                WebdavHost = request.WebdavConfig?.ServerUrl,
                WebdavUsername = request.WebdavConfig?.Username,
                WebdavPasswordEncrypted = TryEncrypt(request.WebdavConfig?.Password),
                TotpMaxFailedAttempts = defaultTotpMaxFailedAttempts,
            };
            await _dbContext.ServerSettings.AddAsync(newSettings);
            await _dbContext.SaveChangesAsync();
            _cache = null;
        }

        private static int? TryParseInt(string? value)
        {
            return int.TryParse(value, out int i) ? i : null;
        }

        private string? TryEncrypt(string? password)
        {
            if (password is null)
            {
                return null;
            }
            byte[] passwordBytes = _crypto.Encrypt(password);
            return Convert.ToBase64String(passwordBytes);
        }
    }
}
