using Cotton.Server.Helpers;
using Cotton.Server.Settings;
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
