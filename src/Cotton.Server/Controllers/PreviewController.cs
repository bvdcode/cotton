using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
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
            PipelineContext context = new()
            {
                StoreInMemoryCache = true
            };
            var stream = await _storage.ReadAsync(previewImageHash, context);
            return File(stream, "image/webp", previewImageHash + ".webp");
        }
    }
}
