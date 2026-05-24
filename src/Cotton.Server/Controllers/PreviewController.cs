// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using DbUser = Cotton.Database.Models.User;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
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
    /// <summary>
    /// Exposes HTTP endpoints for preview operations.
    /// </summary>
    [ApiController]
    [Route(Routes.V1.Previews)]
    public class PreviewController(
        CottonDbContext _dbContext,
        IDatabaseIntegrityVerifier _integrity,
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
                if (!TryParsePreviewToken(previewHashEncryptedHex, out PreviewToken token))
                {
                    return this.ApiNotFound("Preview image not found.");
                }

                string? decryptedPreviewHash = token.Kind switch
                {
                    FileManifest.PreviewTokenPrefix => await GetFilePreviewHashAsync(token),
                    DbUser.AvatarPreviewTokenPrefix => await GetAvatarPreviewHashAsync(token),
                    _ => null
                };

                if (decryptedPreviewHash is null)
                {
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

        private async Task<string?> GetFilePreviewHashAsync(PreviewToken token)
        {
            FileManifest? manifest = await _dbContext.FileManifests
                .SingleOrDefaultAsync(x => x.Id == token.OwnerId, HttpContext.RequestAborted);
            if (manifest?.SmallFilePreviewHashEncrypted is null || manifest.SmallFilePreviewHash is null)
            {
                return null;
            }

            _integrity.RequireValid(_dbContext, manifest, "preview.file-manifest");
            return GetStoredPreviewHash(
                token,
                manifest.SmallFilePreviewHashEncrypted,
                manifest.SmallFilePreviewHash,
                "file preview");
        }

        private async Task<string?> GetAvatarPreviewHashAsync(PreviewToken token)
        {
            DbUser? user = await _dbContext.Users
                .SingleOrDefaultAsync(x => x.Id == token.OwnerId, HttpContext.RequestAborted);
            if (user?.AvatarHashEncrypted is null || user.AvatarHash is null)
            {
                return null;
            }

            _integrity.RequireValid(_dbContext, user, "preview.avatar-user");
            return GetStoredPreviewHash(
                token,
                user.AvatarHashEncrypted,
                user.AvatarHash,
                "avatar preview");
        }

        private string? GetStoredPreviewHash(
            PreviewToken token,
            byte[] storedEncryptedHash,
            byte[] storedPlainHash,
            string previewKind)
        {
            if (!storedEncryptedHash.SequenceEqual(token.EncryptedHash))
            {
                _logger.LogWarning("Rejected {PreviewKind} token for {OwnerId}: encrypted hash does not match the signed row.", previewKind, token.OwnerId);
                return null;
            }

            byte[] decryptedHashBytes;
            try
            {
                decryptedHashBytes = _crypto.Decrypt(token.EncryptedHash);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt {PreviewKind} token for {OwnerId}.", previewKind, token.OwnerId);
                return null;
            }

            if (!storedPlainHash.SequenceEqual(decryptedHashBytes))
            {
                _logger.LogWarning("Rejected {PreviewKind} token for {OwnerId}: decrypted hash does not match the signed row.", previewKind, token.OwnerId);
                return null;
            }

            return Hasher.ToHexStringHash(decryptedHashBytes);
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

            if (!Guid.TryParseExact(value.Substring(1, TokenOwnerIdLength), "N", out Guid ownerId))
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
                token = new PreviewToken(kind, ownerId, Convert.FromHexString(encryptedHashHex));
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private readonly record struct PreviewToken(char Kind, Guid OwnerId, byte[] EncryptedHash);
    }
}
