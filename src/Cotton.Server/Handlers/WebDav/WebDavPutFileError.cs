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
    /// Lists the supported web dav put file error values.
    /// </summary>
    public enum WebDavPutFileError
    {
        /// <summary>
        /// Represents the parent not found option.
        /// </summary>
        ParentNotFound,
        /// <summary>
        /// Represents the is collection option.
        /// </summary>
        IsCollection,
        /// <summary>
        /// Represents the invalid name option.
        /// </summary>
        InvalidName,
        /// <summary>
        /// Represents the conflict option.
        /// </summary>
        Conflict,
        /// <summary>
        /// Represents the precondition failed option.
        /// </summary>
        PreconditionFailed,
        /// <summary>
        /// Represents the upload aborted option.
        /// </summary>
        UploadAborted,
        /// <summary>
        /// Represents the quota exceeded option.
        /// </summary>
        QuotaExceeded,
        /// <summary>
        /// Represents the storage pressure option.
        /// </summary>
        StoragePressure
    }
}
