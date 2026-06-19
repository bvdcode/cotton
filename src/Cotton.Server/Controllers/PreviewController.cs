// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using DbUser = Cotton.Database.Models.User;
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
    /// <summary>
    /// Exposes HTTP endpoints for preview operations.
    /// </summary>
    [ApiController]
    [Route(Routes.V1.Previews)]
    public class PreviewController(
        IStreamCipher _crypto,
        ILogger<PreviewController> _logger,
        IStoragePipeline _storage) : ControllerBase
    {
        private const int TokenOwnerIdLength = 32;
        private static readonly SemaphoreSlim _previewGate = new(8);

        /// <summary>
        /// Gets file preview.
        /// </summary>
        [HttpGet("{previewHashEncryptedHex}")]
        [HttpGet("{previewHashEncryptedHex}.webp")]
        public async Task<IActionResult> GetFilePreview([FromRoute] string previewHashEncryptedHex)
        {
            await _previewGate.WaitAsync(HttpContext.RequestAborted);
            try
            {
                // The token embeds the AES-GCM encrypted preview hash. GCM is authenticated,
                // so a token that decrypts to a valid hash was necessarily issued by this server;
                // that is sufficient to serve a public preview blob without a database round-trip.
                // Only preview/avatar hashes are ever encrypted with this key, so there is no other
                // ciphertext that could be replayed here. Keep this path off the hot database path.
                if (!TryParsePreviewToken(previewHashEncryptedHex, out PreviewToken token))
                {
                    return this.ApiNotFound("Preview image not found.");
                }

                string decryptedPreviewHash;
                try
                {
                    decryptedPreviewHash = Hasher.ToHexStringHash(_crypto.Decrypt(token.EncryptedHash));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decrypt preview token.");
                    return this.ApiNotFound("Preview image not found.");
                }

                if (!Hasher.IsValidHash(decryptedPreviewHash))
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

        private static bool TryParsePreviewToken(string value, out PreviewToken token)
        {
            token = default;
            if (value.Length <= TokenOwnerIdLength + 1)
            {
                return false;
            }

            char kind = value[0];
            if (kind != FileManifest.PreviewTokenPrefix && kind != DbUser.AvatarPreviewTokenPrefix)
            {
                return false;
            }

            if (!Guid.TryParseExact(value.Substring(1, TokenOwnerIdLength), "N", out _))
            {
                return false;
            }

            string encryptedHashHex = value[(TokenOwnerIdLength + 1)..];
            if (encryptedHashHex.Length == 0 || encryptedHashHex.Length % 2 != 0)
            {
                return false;
            }

            try
            {
                token = new PreviewToken(kind, Convert.FromHexString(encryptedHashHex));
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private readonly record struct PreviewToken(char Kind, byte[] EncryptedHash);
    }
}
