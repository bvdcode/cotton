// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Jobs;
using Cotton.Server.Models;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using Cotton.Validators;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Quartz.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Quartz;
using System.Security.Cryptography;

namespace Cotton.Server.Handlers.WebDav
{
    /// <summary>
    /// Result of WebDAV PUT operation
    /// </summary>
    public record WebDavPutFileResult(
        bool Success,
        bool Created,
        WebDavPutFileError? Error = null,
        Guid? NodeFileId = null);
}
