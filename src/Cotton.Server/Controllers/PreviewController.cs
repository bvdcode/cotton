using Cotton.Storage.Abstractions;
using EasyExtensions.AspNetCore.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class PreviewController(IStoragePipeline _storage) : ControllerBase
    {
        [HttpGet("/api/v1/preview/{previewImageHash}")]
        [HttpGet("/api/v1/preview/{previewImageHash}.webp")]
        public async Task<IActionResult> GetFilePreview([FromRoute] string previewImageHash)
        {
            bool exists = await _storage.ExistsAsync(previewImageHash);
            if (!exists)
            {
                return this.ApiNotFound("Preview image not found.");
            }
            var stream = await _storage.ReadAsync(previewImageHash);
            return File(stream, "image/webp", previewImageHash + ".webp");
        }
    }
}
