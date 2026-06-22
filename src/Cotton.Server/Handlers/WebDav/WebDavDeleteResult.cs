// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Nodes;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.WebDav
{
    /// <summary>
    /// Result of WebDAV DELETE operation
    /// </summary>
    public record WebDavDeleteResult(
        bool Success,
        bool NotFound = false,
        Guid? DeletedNodeId = null,
        Guid? DeletedNodeFileId = null);
}
