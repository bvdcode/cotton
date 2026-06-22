// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Extensions;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Services.WebDav;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.WebDav
{
    /// <summary>
    /// Result of WebDAV GET operation
    /// </summary>
    public record WebDavGetFileResult(
        bool Found,
        bool IsCollection,
        Stream? Content = null,
        string? ContentType = null,
        long ContentLength = 0,
        string? FileName = null,
        DateTimeOffset? LastModified = null,
        string? ETag = null);
}
