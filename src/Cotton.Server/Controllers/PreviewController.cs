using Cotton.Server.Extensions;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Previews)]
    public class PreviewController(
        IStreamCipher _crypto,
        ILogger<PreviewController> _logger,
        IStoragePipeline _storage) : ControllerBase
    {
        private static readonly SemaphoreSlim _previewGate = new(8);

        [HttpGet("{previewHashEncryptedHex}")]
        [HttpGet("{previewHashEncryptedHex}.webp")]
        public async Task<IActionResult> GetFilePreview([FromRoute] string previewHashEncryptedHex)
        {
            await _previewGate.WaitAsync(HttpContext.RequestAborted);
            try
            {
                string? decryptedPreviewHash;
                try
                {
                    byte[] encrypted = Convert.FromHexString(previewHashEncryptedHex);
                    var hashBytes = _crypto.Decrypt(encrypted);
                    decryptedPreviewHash = Hasher.ToHexStringHash(hashBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt preview token {Token}", previewHashEncryptedHex);
                    return this.ApiNotFound("Preview image not found.");
                }
                bool validHash = Hasher.IsValidHash(decryptedPreviewHash);
                if (!validHash)
                {
                    _logger.LogWarning("Decrypted preview hash is invalid: {Hash}", decryptedPreviewHash);
                    return this.ApiNotFound("Preview image not found.");
                }
                bool exists = await _storage.ExistsAsync(decryptedPreviewHash);
                if (!exists)
                {
                    _logger.LogWarning("Preview image not found for hash: {Hash}", decryptedPreviewHash);
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
            finally
            {
                _previewGate.Release();
            }
        }
    }
}
