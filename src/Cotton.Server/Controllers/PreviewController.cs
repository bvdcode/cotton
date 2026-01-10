using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class PreviewController(
        IStreamCipher _crypto,
        IStoragePipeline _storage) : ControllerBase
    {
        [HttpGet("/api/v1/preview/{encryptedFilePreviewHashHex}")]
        [HttpGet("/api/v1/preview/{encryptedFilePreviewHashHex}.webp")]
        public async Task<IActionResult> GetFilePreview([FromRoute] string encryptedFilePreviewHashHex)
        {
            string? decryptedPreviewHash;
            try
            {
                byte[] encryptedPreviewHash = Convert.FromHexString(encryptedFilePreviewHashHex);
                decryptedPreviewHash = _crypto.Decrypt(encryptedPreviewHash);
            }
            catch (Exception)
            {
                return this.ApiNotFound("Preview image not found.");
            }
            bool validHash = Hasher.IsValidHash(decryptedPreviewHash);
            if (!validHash)
            {
                return this.ApiNotFound("Preview image not found.");
            }
            bool exists = await _storage.ExistsAsync(decryptedPreviewHash);
            if (!exists)
            {
                return this.ApiNotFound("Preview image not found.");
            }
            string etag = $"\"sha256-{decryptedPreviewHash}\"";
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
            var stream = await _storage.ReadAsync(decryptedPreviewHash, context);
            return File(stream, "image/webp");
        }
    }
}
