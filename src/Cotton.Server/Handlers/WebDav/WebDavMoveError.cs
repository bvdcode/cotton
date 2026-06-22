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
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cotton.Server.Handlers.WebDav
{
    /// <summary>
    /// Lists the supported web dav move error values.
    /// </summary>
    public enum WebDavMoveError
    {
        /// <summary>
        /// Represents the source not found option.
        /// </summary>
        SourceNotFound,
        /// <summary>
        /// Represents the destination parent not found option.
        /// </summary>
        DestinationParentNotFound,
        /// <summary>
        /// Represents the destination exists option.
        /// </summary>
        DestinationExists,
        /// <summary>
        /// Represents the invalid name option.
        /// </summary>
        InvalidName,
        /// <summary>
        /// Represents the cannot move root option.
        /// </summary>
        CannotMoveRoot,
        /// <summary>
        /// Represents the cannot move into descendant option.
        /// </summary>
        CannotMoveIntoDescendant
    }
}
