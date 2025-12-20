// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using Cotton.Server.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class ServerController(CottonSettingsService _settings) : ControllerBase
    {
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
