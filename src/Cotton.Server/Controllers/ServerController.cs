// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class ServerController(SettingsProvider _settings) : ControllerBase
    {
        [HttpGet("/api/v1/server-info")]
        public IActionResult GetServerInfo()
        {
            string version = Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev";
            return Ok(new
            {
                version,
                time = DateTime.UtcNow,
                product = "Cotton Cloud",
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("/api/v1/settings")]
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
        [HttpGet("/api/v1/settings")]
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
