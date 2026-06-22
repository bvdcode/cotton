// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Handlers.Nodes;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.WebDav
{
    /// <summary>
    /// Result of WebDAV COPY operation
    /// </summary>
    public record WebDavCopyResult(
        bool Success,
        bool Created,
        WebDavCopyError? Error = null,
        Guid? CopiedNodeId = null,
        Guid? CopiedNodeFileId = null);
}
