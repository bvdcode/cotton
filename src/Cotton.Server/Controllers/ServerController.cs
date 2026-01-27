// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Shared;
using Cotton.Shared.Models;
using EasyExtensions.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Server)]
    public class ServerController(SettingsProvider _settings) : ControllerBase
    {
        [Authorize(Roles = "Admin")]
        [HttpPost("emergency-shutdown")]
        public IActionResult EmergencyShutdown()
        {
            Environment.Exit(1);
            return Ok();
        }

        [HttpGet("info")]
        public IActionResult GetServerInfo()
        {
            string instanceIdHash = _settings.GetServerSettings().InstanceId.ToString().Sha256();
            string version = Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev";
            return Ok(new PublicServerInfo()
            {
                Version = version,
                CurrentTime = DateTime.UtcNow,
                Product = Constants.ProductName,
                InstanceIdHash = instanceIdHash,
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("settings")]
        public async Task<IActionResult> CreateSettings(ServerSettingsRequestDto request)
        {
            string? error = await _settings.ValidateServerSettingsAsync(request);
            if (error is not null)
            {
                return BadRequest(new { error });
            }
            await _settings.SaveServerSettingsAsync(request);
            return Ok();
        }

        [Authorize]
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            bool serverHasUsers = await _settings.ServerHasUsersAsync();
            bool isServerInitialized = await _settings.IsServerInitializedAsync();
            int maxChunkSizeBytes = _settings.GetServerSettings().MaxChunkSizeBytes;
            var settings = new
            {
                serverHasUsers,
                maxChunkSizeBytes,
                isServerInitialized,
                Hasher.SupportedHashAlgorithm,
            };
            return Ok(settings);
        }
    }
}
