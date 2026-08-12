// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Helpers;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using EasyExtensions.Helpers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using System.Net;
using System.Text.Json;

namespace Cotton.Server.Handlers.Files
{
    public record ShareFileResult
    {
        public string Kind { get; init; } = "";

        public string? RedirectUrl { get; init; }

        public string? HtmlContent { get; init; }

        public string? ContentType { get; init; }

        public long? ContentLength { get; init; }

        public string? EntityTag { get; init; }

        public string? FileName { get; init; }

        public bool? Inline { get; init; }

        public Stream? FileStream { get; init; }

        public string? DownloadName { get; init; }

        public DateTimeOffset? LastModified { get; init; }

        public EntityTagHeaderValue? EntityTagValue { get; init; }

        public bool DeleteAfterUse { get; init; }

        public Guid? DeleteTokenId { get; init; }

        public string? ErrorMessage { get; init; }

        public bool IsTokenLookupFailure { get; init; }

        public static ShareFileResult AsBadRequest(string message) => new() { Kind = "badRequest", ErrorMessage = message };

        public static ShareFileResult AsNotFound(string message) => new() { Kind = "notFound", ErrorMessage = message };

        public static ShareFileResult AsTokenNotFound(string message) =>
            new() { Kind = "notFound", ErrorMessage = message, IsTokenLookupFailure = true };

        public static ShareFileResult AsRedirect(string url) => new() { Kind = "redirect", RedirectUrl = url };

        public static ShareFileResult AsTokenNotFoundRedirect(string url) =>
            new() { Kind = "redirect", RedirectUrl = url, IsTokenLookupFailure = true };

        public static ShareFileResult AsHtml(string html) => new() { Kind = "html", HtmlContent = html };

        public static ShareFileResult AsHead(string contentType, long contentLength, string entityTag, string fileName, bool inline) =>
            new()
            {
                Kind = "head",
                ContentType = contentType,
                ContentLength = contentLength,
                EntityTag = entityTag,
                FileName = fileName,
                Inline = inline,
            };

        public static ShareFileResult AsStream(Stream stream, string contentType, string fileName, string? downloadName, DateTimeOffset lastModified, EntityTagHeaderValue entityTag, bool deleteAfterUse, Guid deleteTokenId) =>
            new()
            {
                Kind = "stream",
                FileStream = stream,
                ContentType = contentType,
                FileName = fileName,
                DownloadName = downloadName,
                LastModified = lastModified,
                EntityTagValue = entityTag,
                DeleteAfterUse = deleteAfterUse,
                DeleteTokenId = deleteTokenId,
            };
    }
}
