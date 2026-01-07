using Cotton.Storage.Abstractions;
using EasyExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class PreviewController(IStoragePipeline _storage) : ControllerBase
    {
        [Authorize]
        [HttpGet("/api/v1/preview/{previewImageHash}")]
        public async Task<IActionResult> GetFilePreview([FromRoute] string previewImageHash)
        {
            bool exists = await _storage.ExistsAsync(previewImageHash);
            return Ok();
        }
    }
}
