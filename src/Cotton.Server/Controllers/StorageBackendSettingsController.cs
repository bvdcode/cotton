// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for storage backend settings.
    /// </summary>
    [ApiController]
    [Route(Routes.V1.Settings)]
    [Route(Routes.V1.Server + "/settings")]
    public class StorageBackendSettingsController(
        SettingsProvider settings,
        ServerSettingsValidator _validator) : SettingsControllerBase(settings)
    {
        /// <summary>
        /// Sets storage type.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("storage-type/{type}")]
        public async Task<IActionResult> SetStorageType(
            [FromRoute] StorageType type,
            CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(await _validator.ValidateStorageTypeAsync(type, cancellationToken));
            await Settings.SetPropertyAsync(
                x => x.StorageType,
                type,
                GetFallbackPublicBaseUrl(),
                cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets storage type.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("storage-type")]
        public IActionResult GetStorageType()
        {
            StorageType storageType = Settings.GetServerSettings().StorageType;
            return Ok(new { storageType = storageType.ToString() });
        }

        /// <summary>
        /// Sets S3 config.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("s3-config")]
        public async Task<IActionResult> SetS3Config(
            [FromBody] S3Config s3Config,
            CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(await _validator.ValidateS3ConfigAsync(s3Config, cancellationToken));
            await Settings.UpdateSettingsAsync(settings =>
            {
                settings.S3AccessKeyId = s3Config.AccessKey.Trim();
                settings.S3SecretAccessKeyEncrypted = s3Config.SecretKey;
                settings.S3EndpointUrl = s3Config.Endpoint.Trim().TrimEnd('/');
                settings.S3Region = s3Config.Region.Trim();
                settings.S3BucketName = s3Config.Bucket.Trim();
            }, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Gets S3 config.
        /// </summary>
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("s3-config")]
        public IActionResult GetS3Config()
        {
            ServerSettingsSnapshot settings = Settings.GetServerSettings();
            S3Config s3Config = new()
            {
                AccessKey = settings.S3AccessKeyId ?? string.Empty,
                SecretKey = string.Empty,
                Endpoint = settings.S3EndpointUrl ?? string.Empty,
                Region = settings.S3Region ?? string.Empty,
                Bucket = settings.S3BucketName ?? string.Empty
            };
            return Ok(s3Config);
        }
    }
}
