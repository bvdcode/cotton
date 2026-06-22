// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.WebDav
{
    /// <summary>
    /// Result of WebDAV HEAD operation
    /// </summary>
    public record WebDavHeadResult(
        bool Found,
        bool IsCollection,
        string? ContentType = null,
        long ContentLength = 0,
        DateTimeOffset? LastModified = null,
        string? ETag = null);
}
