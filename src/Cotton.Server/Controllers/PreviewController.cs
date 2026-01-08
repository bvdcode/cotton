using Cotton.Database;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using EasyExtensions.AspNetCore.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class PreviewController(IStoragePipeline _storage, CottonDbContext _dbContext) : ControllerBase
    {
        [HttpGet("/api/v1/preview/{previewId:guid}")]
        [HttpGet("/api/v1/preview/{previewId:guid}.webp")]
        public async Task<IActionResult> GetFilePreview([FromRoute] Guid previewId)
        {
            var found = await _dbContext.FilePreviews
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == previewId);
            if (found == null)
            {
                return this.ApiNotFound("Preview not found.");
            }
            string previewImageHash = Hasher.ToHexStringHash(found.Hash);
            bool validHash = Hasher.IsValidHash(previewImageHash);
            if (!validHash)
            {
                return this.ApiBadRequest("Invalid preview image hash.");
            }
            bool exists = await _storage.ExistsAsync(previewImageHash);
            if (!exists)
            {
                return this.ApiNotFound("Preview image not found.");
            }
            string etag = $"\"sha256-{previewImageHash}\"";
            var etagHeader = new EntityTagHeaderValue(etag);
            if (Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var inmValues))
            {
                var clientEtags = EntityTagHeaderValue.ParseList([.. inmValues!]);
                if (clientEtags.Any(x => x.Compare(etagHeader, useStrongComparison: true)))
                {
                    Response.Headers.ETag = etagHeader.ToString();
                    Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }
            PipelineContext context = new()
            {
                StoreInMemoryCache = true
            };
            Response.Headers.ETag = etag;
            Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            var stream = await _storage.ReadAsync(previewImageHash, context);
            return File(stream, "image/webp");
        }
    }
}
