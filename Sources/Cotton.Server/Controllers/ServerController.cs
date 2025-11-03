// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Shared;
using Cotton.Server.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class ServerController(CottonSettings _settings) : ControllerBase
    {
        [HttpGet("/api/v1/settings")]
        public IActionResult GetSettings()
        {
            var settings = new
            {
                _settings.MaxChunkSizeBytes,
                HashHelpers.SupportedHashAlgorithm,
            };
            return Ok(settings);
        }
    }
}
