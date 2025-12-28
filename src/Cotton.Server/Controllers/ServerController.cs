// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class ServerController(SettingsProvider _settings) : ControllerBase
    {
        [HttpPost("/api/v1/settings")]
        public async Task<IActionResult> CreateSettings()
        {

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
