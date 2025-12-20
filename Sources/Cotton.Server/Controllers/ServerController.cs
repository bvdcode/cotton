// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using Cotton.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class ServerController(CottonSettingsService _settings) : ControllerBase
    {
        [HttpGet("/api/v1/settings")]
        public IActionResult GetSettings()
        {
            int maxChunkSizeBytes = _settings.GetServerSettings().MaxChunkSizeBytes;
            var settings = new
            {
                maxChunkSizeBytes,
                Hasher.SupportedHashAlgorithm,
            };
            return Ok(settings);
        }
    }
}
