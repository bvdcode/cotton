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
    /// <summary>
    /// Represents the result of share file.
    /// </summary>
    public record ShareFileResult
    {
        /// <summary>
        /// Gets or sets the kind.
        /// </summary>
        public string Kind { get; init; } = "";
        /// <summary>
        /// Gets or sets the redirect url.
        /// </summary>
        public string? RedirectUrl { get; init; }
        /// <summary>
        /// Gets or sets the html content.
        /// </summary>
        public string? HtmlContent { get; init; }

        /// <summary>
        /// Gets or sets the response content type.
        /// </summary>
        public string? ContentType { get; init; }
        /// <summary>
        /// Gets or sets the response content length in bytes.
        /// </summary>
        public long? ContentLength { get; init; }
        /// <summary>
        /// Gets or sets the entity tag.
        /// </summary>
        public string? EntityTag { get; init; }
        /// <summary>
        /// Gets or sets the file name shown to clients.
        /// </summary>
        public string? FileName { get; init; }
        /// <summary>
        /// Gets or sets the inline.
        /// </summary>
        public bool? Inline { get; init; }

        /// <summary>
        /// Gets or sets the file stream.
        /// </summary>
        public Stream? FileStream { get; init; }
        /// <summary>
        /// Gets or sets the download name.
        /// </summary>
        public string? DownloadName { get; init; }
        /// <summary>
        /// Gets or sets the last modified.
        /// </summary>
        public DateTimeOffset? LastModified { get; init; }
        /// <summary>
        /// Gets or sets the entity tag value.
        /// </summary>
        public EntityTagHeaderValue? EntityTagValue { get; init; }
        /// <summary>
        /// Deletes after use.
        /// </summary>
        public bool DeleteAfterUse { get; init; }
        /// <summary>
        /// Deletes token id.
        /// </summary>
        public Guid? DeleteTokenId { get; init; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Converts the result to bad request.
        /// </summary>
        public static ShareFileResult AsBadRequest(string message) => new() { Kind = "badRequest", ErrorMessage = message };
        /// <summary>
        /// Converts the result to not found.
        /// </summary>
        public static ShareFileResult AsNotFound(string message) => new() { Kind = "notFound", ErrorMessage = message };
        /// <summary>
        /// Converts the result to redirect.
        /// </summary>
        public static ShareFileResult AsRedirect(string url) => new() { Kind = "redirect", RedirectUrl = url };
        /// <summary>
        /// Converts the result to html.
        /// </summary>
        public static ShareFileResult AsHtml(string html) => new() { Kind = "html", HtmlContent = html };
        /// <summary>
        /// Converts the result to head.
        /// </summary>
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

        /// <summary>
        /// Converts the result to stream.
        /// </summary>
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
