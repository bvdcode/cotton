// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Helpers;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Cotton.Server.Models;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Settings)]
    [Route(Routes.V1.Server + "/settings")]
    public class SettingsController(
        SettingsProvider settings,
        ServerSettingsValidator _validator) : SettingsControllerBase(settings)
    {

        [HttpGet]
        [Authorize]
        public IActionResult GetClientSettings()
        {
            ServerSettingsSnapshot settings = Settings.GetServerSettings();
            string? currentVersion = AppVersionHelpers.GetAppVersion();
            return Ok(new
            {
                Version = currentVersion,
                settings.MaxChunkSizeBytes,
                Hasher.SupportedHashAlgorithm,
            });
        }

        [HttpGet("is-setup-complete")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> IsServerInitialized()
        {
            bool isServerInitialized = await Settings.IsServerInitializedAsync();
            return Ok(new { IsServerInitialized = isServerInitialized });
        }


        [Authorize]
        [HttpGet("supported-hash-algorithms")]
        public IActionResult GetSupportedHashAlgorithms()
        {
            return Ok(new { supportedHashAlgorithms = new string[] { Hasher.SupportedHashAlgorithm } });
        }


        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("server-usage")]
        public async Task<IActionResult> SetServerUsage([FromBody] JsonElement usage, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ServerUsage[] parsedUsage = ParseServerUsage(usage);
            await Settings.SetPropertyAsync(x => x.ServerUsage, parsedUsage, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("server-usage")]
        public IActionResult GetServerUsage()
        {
            string[] serverUsage = [.. Settings.GetServerSettings().ServerUsage.Select(x => x.ToString())];
            return Ok(new { serverUsage });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("telemetry")]
        public async Task<IActionResult> SetTelemetry([FromBody] bool enabled, CancellationToken cancellationToken = default)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_validator.ValidateTelemetryChange(enabled));
            await Settings.SetPropertyAsync(x => x.TelemetryEnabled, enabled, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("telemetry")]
        public IActionResult GetTelemetry()
        {
            bool telemetryEnabled = Settings.GetServerSettings().TelemetryEnabled;
            return Ok(new { telemetryEnabled });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("disable-version-check")]
        public async Task<IActionResult> SetDisableVersionCheck(
            [FromBody] bool disabled,
            CancellationToken cancellationToken = default)
        {
            await EnsureSettingsAsync(cancellationToken);
            await Settings.SetPropertyAsync(
                settings => settings.DisableVersionCheck,
                disabled,
                GetFallbackPublicBaseUrl(),
                cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("disable-version-check")]
        public IActionResult GetDisableVersionCheck()
        {
            bool disableVersionCheck = Settings.GetServerSettings().DisableVersionCheck;
            return Ok(new { disableVersionCheck });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("storage-space-mode/{mode}")]
        public async Task<IActionResult> SetStorageSpaceMode([FromRoute] StorageSpaceMode mode, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(Enum.IsDefined(mode) ? null : "Invalid storage space mode: " + mode);
            await Settings.SetPropertyAsync(x => x.StorageSpaceMode, mode, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("storage-space-mode")]
        public IActionResult GetStorageSpaceMode()
        {
            StorageSpaceMode storageSpaceMode = Settings.GetServerSettings().StorageSpaceMode;
            return Ok(new { storageSpaceMode = storageSpaceMode.ToString() });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("default-user-storage-quota-bytes")]
        public async Task<IActionResult> SetDefaultUserStorageQuotaBytes([FromBody] long? quotaBytes, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_validator.ValidateDefaultUserStorageQuotaBytes(quotaBytes));
            long? normalizedQuotaBytes = quotaBytes is null or 0 ? null : quotaBytes;
            await Settings.SetPropertyAsync(
                x => x.DefaultUserStorageQuotaBytes,
                normalizedQuotaBytes,
                GetFallbackPublicBaseUrl(),
                cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("default-user-storage-quota-bytes")]
        public IActionResult GetDefaultUserStorageQuotaBytes()
        {
            long? defaultUserStorageQuotaBytes = Settings.GetServerSettings().DefaultUserStorageQuotaBytes;
            return Ok(new { defaultUserStorageQuotaBytes });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("default-user-template-node")]
        public async Task<IActionResult> SetDefaultUserTemplateNode([FromBody] Guid? nodeId, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            Guid? normalizedNodeId = nodeId is null || nodeId == Guid.Empty ? null : nodeId;
            Guid ownerId = User.GetUserId();
            ThrowIfInvalid(await _validator.ValidateDefaultUserTemplateNodeIdAsync(normalizedNodeId, ownerId, cancellationToken));
            await Settings.SetPropertyAsync(
                x => x.DefaultUserTemplateNodeId,
                normalizedNodeId,
                GetFallbackPublicBaseUrl(),
                cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("default-user-template-node")]
        public IActionResult GetDefaultUserTemplateNode()
        {
            Guid? defaultUserTemplateNodeId = Settings.GetServerSettings().DefaultUserTemplateNodeId;
            return Ok(new { defaultUserTemplateNodeId });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("timezone")]
        public async Task<IActionResult> SetTimezone([FromBody] string? timezone, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_validator.ValidateTimezone(timezone));
            await Settings.SetPropertyAsync(x => x.Timezone, timezone!.Trim(), GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("timezone")]
        public IActionResult GetTimezone()
        {
            string timezone = Settings.GetServerSettings().Timezone;
            return Ok(new { timezone });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("public-base-url")]
        public async Task<IActionResult> SetPublicBaseUrl([FromBody] string? url, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_validator.ValidatePublicBaseUrl(url));
            await Settings.SetPropertyAsync(
                x => x.PublicBaseUrl,
                SettingsProvider.NormalizePublicBaseUrl(url),
                GetFallbackPublicBaseUrl(),
                cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("public-base-url")]
        public IActionResult GetPublicBaseUrl()
        {
            string? publicBaseUrl = Settings.GetServerSettings().PublicBaseUrl;
            return Ok(new { publicBaseUrl });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("compution-mode/{mode}")]
        public async Task<IActionResult> SetComputionMode([FromRoute] ComputionMode mode, CancellationToken cancellationToken)
        {
            await EnsureSettingsAsync(cancellationToken);
            ThrowIfInvalid(_validator.ValidateComputionMode(mode));
            await Settings.SetPropertyAsync(x => x.ComputionMode, mode, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("compution-mode")]
        public IActionResult GetComputionMode()
        {
            ComputionMode computionMode = Settings.GetServerSettings().ComputionMode;
            return Ok(new { computionMode = computionMode.ToString() });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("allow-cross-user-deduplication")]
        public async Task<IActionResult> SetAllowCrossUserDeduplication([FromBody] bool allow, CancellationToken cancellationToken)
        {
            await Settings.SetPropertyAsync(x => x.AllowCrossUserDeduplication, allow, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("allow-cross-user-deduplication")]
        public IActionResult GetAllowCrossUserDeduplication()
        {
            bool allowCrossUserDeduplication = Settings.GetServerSettings().AllowCrossUserDeduplication;
            return Ok(new { allowCrossUserDeduplication });
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPatch("allow-global-indexing")]
        public async Task<IActionResult> SetAllowGlobalIndexing([FromBody] bool allow, CancellationToken cancellationToken)
        {
            await Settings.SetPropertyAsync(x => x.AllowGlobalIndexing, allow, GetFallbackPublicBaseUrl(), cancellationToken);
            return NoContent();
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("allow-global-indexing")]
        public IActionResult GetAllowGlobalIndexing()
        {
            bool allowGlobalIndexing = Settings.GetServerSettings().AllowGlobalIndexing;
            return Ok(new { allowGlobalIndexing });
        }

        private static ServerUsage[] ParseServerUsage(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new BadRequestException<CottonServerSettings>("Server usage must be an array.");
            }

            var result = new List<ServerUsage>();
            foreach (JsonElement item in value.EnumerateArray())
            {
                ServerUsage usage;
                if (item.ValueKind == JsonValueKind.String)
                {
                    string? raw = item.GetString();
                    if (!Enum.TryParse(raw, ignoreCase: true, out usage))
                    {
                        throw new BadRequestException<CottonServerSettings>("Invalid server usage: " + raw);
                    }
                }
                else if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out int rawValue))
                {
                    usage = (ServerUsage)rawValue;
                    if (!Enum.IsDefined(usage))
                    {
                        throw new BadRequestException<CottonServerSettings>("Invalid server usage: " + rawValue);
                    }
                }
                else
                {
                    throw new BadRequestException<CottonServerSettings>("Server usage entries must be strings or numbers.");
                }

                if (!result.Contains(usage))
                {
                    result.Add(usage);
                }
            }

            return result.Count == 0 ? [ServerUsage.Other] : [.. result];
        }
    }
}
