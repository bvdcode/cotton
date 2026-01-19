// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Amazon.S3;
using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models.Dto;
using EasyExtensions.Abstractions;
using EasyExtensions.Extensions;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Providers
{
    public class SettingsProvider(
        IStreamCipher _crypto,
        CottonDbContext _dbContext)
    {
        private static CottonServerSettings? _cache;
        private const int defaultSessionTimeoutHours = 24 * 30;
        private const int defaultTotpMaxFailedAttempts = 64;
        private const int defaultEncryptionThreads = 2;
        private const int defaultMaxChunkSizeBytes = 4 * 1024 * 1024;
        private const int defaultCipherChunkSizeBytes = 1 * 1024 * 1024;
        public CottonServerSettings GetServerSettings()
        {
            if (_cache != null)
            {
                return _cache;
            }
            var settings = _dbContext.ServerSettings
                .AsNoTracking()
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefault();
            if (settings is not null)
            {
                return settings;
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
            return _cache.Adapt<CottonServerSettings>();
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
            bool tzExists = TimeZoneInfo.TryFindSystemTimeZoneById(request.Timezone, out var _);
            if (!tzExists)
            {
                return "Timezone not found: " + request.Timezone;
            }
            if (!request.Telemetry)
            {
                if (request.Email == EmailMode.Cloud)
                {
                    return "Telemetry must be enabled to use cloud email service.";
                }
                if (request.ComputionMode == ComputionMode.Cloud)
                {
                    return "Telemetry must be enabled to use cloud AI service.";
                }
            }
            if (request.Email == EmailMode.Custom)
            {
                if (request.EmailConfig is null)
                {
                    return "EmailConfig must be provided when using Custom email service.";
                }
            }
            if (request.Storage == StorageType.S3)
            {
                if (request.S3Config is null)
                {
                    return "S3Config must be provided when using S3 storage.";
                }
                try
                {
                    await ValidateS3Async(request.S3Config);
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }
            if (request.ImportSource != ImportSource.None)
            {
                if (request.ImportSource == ImportSource.Webdav && request.WebdavConfig is null)
                {
                    return "WebdavConfig must be provided when using Webdav import source.";
                }
            }
            return null;
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
            int? smtpPort = null;
            if (request.EmailConfig?.Port is not null)
            {
                bool parsed = int.TryParse(request.EmailConfig.Port, out int port);
                if (parsed)
                {
                    smtpPort = port;
                }
            }
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
