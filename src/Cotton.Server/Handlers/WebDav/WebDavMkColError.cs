// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.WebDav
{
    /// <summary>
    /// Lists the supported web dav mk col error values.
    /// </summary>
    public enum WebDavMkColError
    {
        /// <summary>
        /// Represents the parent not found option.
        /// </summary>
        ParentNotFound,
        /// <summary>
        /// Represents the already exists option.
        /// </summary>
        AlreadyExists,
        /// <summary>
        /// Represents the invalid name option.
        /// </summary>
        InvalidName,
        /// <summary>
        /// Represents the conflict option.
        /// </summary>
        Conflict
    }
}
