// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database.Models;
using Cotton.Previews;
using Cotton.Server.Abstractions;
using Cotton.Storage.Abstractions;
using System.Buffers;
using System.Net;

namespace Cotton.Server.Services
{
    public class OidcAvatarImportService(
        HttpClient _httpClient,
        IChunkIngestService _chunkIngest,
        IStreamCipher _crypto,
        ILogger<OidcAvatarImportService> _logger)
    {
        public const int MaxAvatarBytes = 5 * 1024 * 1024;

        private static readonly ImagePreviewGenerator _avatarGenerator = new();

        public async Task TryImportMissingAvatarAsync(
            User user,
            string? pictureUrl,
            string issuer,
            CancellationToken ct)
        {
            if (user.AvatarHash is not null || user.AvatarHashEncrypted is not null)
            {
                return;
            }

            Uri? avatarUri = CreateHttpsUri(pictureUrl);
            if (avatarUri is null)
            {
                return;
            }

            try
            {
                DnsEndPoint? trustedPrivateEndpoint = CreateTrustedPrivateEndpoint(issuer);
                byte[] sourceImage = await DownloadAvatarAsync(
                    avatarUri,
                    trustedPrivateEndpoint,
                    ct);
                byte[] avatarPreviewWebP = await GenerateAvatarPreviewAsync(sourceImage);
                Chunk avatarChunk = await _chunkIngest.UpsertChunkAsync(
                    user.Id,
                    avatarPreviewWebP,
                    avatarPreviewWebP.Length,
                    ct);

                user.AvatarHash = avatarChunk.Hash;
                user.AvatarHashEncrypted = _crypto.Encrypt(avatarChunk.Hash);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to import OIDC avatar from {AvatarHost}",
                    avatarUri.Host);
            }
        }

        private static Uri? CreateHttpsUri(string? pictureUrl)
        {
            if (string.IsNullOrWhiteSpace(pictureUrl))
            {
                return null;
            }

            if (!Uri.TryCreate(pictureUrl.Trim(), UriKind.Absolute, out Uri? parsed))
            {
                return null;
            }

            return string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? parsed
                : null;
        }

        private async Task<byte[]> DownloadAvatarAsync(
            Uri uri,
            DnsEndPoint? trustedPrivateEndpoint,
            CancellationToken ct)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            if (trustedPrivateEndpoint is not null)
            {
                request.Options.Set(
                    OidcAvatarConnectionPolicy.TrustedPrivateEndpointOption,
                    trustedPrivateEndpoint);
            }

            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Avatar download failed with HTTP {(int)response.StatusCode}.");
            }

            long? contentLength = response.Content.Headers.ContentLength;
            if (contentLength > MaxAvatarBytes)
            {
                throw new InvalidOperationException("Avatar image is too large.");
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
            return await ReadLimitedAsync(stream, ct);
        }

        private static DnsEndPoint? CreateTrustedPrivateEndpoint(string issuer)
        {
            Uri? issuerUri = CreateHttpsUri(issuer);
            return issuerUri is null
                ? null
                : new DnsEndPoint(issuerUri.IdnHost, issuerUri.Port);
        }

        private static async Task<byte[]> ReadLimitedAsync(Stream stream, CancellationToken ct)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            try
            {
                using var output = new MemoryStream();
                while (true)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                    if (read == 0)
                    {
                        return output.ToArray();
                    }

                    if (output.Length + read > MaxAvatarBytes)
                    {
                        throw new InvalidOperationException("Avatar image is too large.");
                    }

                    output.Write(buffer, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static async Task<byte[]> GenerateAvatarPreviewAsync(byte[] sourceImage)
        {
            await using var sourceStream = new MemoryStream(sourceImage, writable: false);
            return await _avatarGenerator.GeneratePreviewWebPAsync(
                sourceStream,
                PreviewGeneratorProvider.DefaultSmallPreviewSize);
        }
    }
}
