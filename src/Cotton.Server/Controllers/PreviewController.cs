// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database.Models;
using DbUser = Cotton.Database.Models.User;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using EasyExtensions.AspNetCore.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [DisableRateLimiting]
    [Route(Routes.V1.Previews)]
    public class PreviewController(
        IStreamCipher _crypto,
        ILogger<PreviewController> _logger,
        IStoragePipeline _storage) : ControllerBase
    {
        private const int TokenOwnerIdLength = 32;
        internal const int PreviewConcurrencyLimit = 8;
        private static readonly SemaphoreSlim _previewGate = new(
            PreviewConcurrencyLimit,
            PreviewConcurrencyLimit);

        [HttpGet("{previewHashEncryptedHex}")]
        [HttpGet("{previewHashEncryptedHex}.webp")]
        public async Task<IActionResult> GetFilePreview([FromRoute] string previewHashEncryptedHex)
        {
            await _previewGate.WaitAsync(HttpContext.RequestAborted);
            var gateLease = new PreviewGateLease(_previewGate);
            try
            {
                Response.RegisterForDispose(gateLease);

                // The token embeds the AES-GCM encrypted preview hash. GCM is authenticated,
                // so a token that decrypts to a valid hash was necessarily issued by this server;
                // that is sufficient to serve a public preview blob without a database round-trip.
                // Only preview/avatar hashes are ever encrypted with this key, so there is no other
                // ciphertext that could be replayed here. Stale tokens remain readable until storage
                // GC removes their blob; keep this path off the hot database path.
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
                EntityTagHeaderValue etagHeader = new(etag);
                if (FileETags.MatchesIfNoneMatchHeader(Request, etagHeader))
                {
                    Response.Headers.ETag = etagHeader.ToString();
                    Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                    return StatusCode(StatusCodes.Status304NotModified);
                }
                PipelineContext context = new()
                {
                    StoreInMemoryCache = true
                };
                Response.Headers.ETag = etag;
                Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                Stream stream = await _storage.ReadAsync(decryptedPreviewHash, context);
                return File(stream, "image/webp");
            }
            catch
            {
                gateLease.Dispose();
                throw;
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

        private class PreviewGateLease(SemaphoreSlim gate) : IDisposable
        {
            private SemaphoreSlim? _gate = gate;

            public void Dispose()
            {
                Interlocked.Exchange(ref _gate, null)?.Release();
            }
        }
    }
}
