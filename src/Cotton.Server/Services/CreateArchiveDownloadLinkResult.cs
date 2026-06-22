// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Validators;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Represents the result of create archive download link.
    /// </summary>
    public class CreateArchiveDownloadLinkResult
    {
        private CreateArchiveDownloadLinkResult(ArchiveDownloadLinkDto? link, string? error, int statusCode)
        {
            Link = link;
            Error = error;
            StatusCode = statusCode;
        }

        /// <summary>
        /// Gets the link.
        /// </summary>
        public ArchiveDownloadLinkDto? Link { get; }
        /// <summary>
        /// Gets the error.
        /// </summary>
        public string? Error { get; }
        /// <summary>
        /// Gets the status code.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Creates a successful operation result.
        /// </summary>
        public static CreateArchiveDownloadLinkResult Success(ArchiveDownloadLinkDto link) => new(link, null, StatusCodes.Status200OK);
        /// <summary>
        /// Creates a bad request result.
        /// </summary>
        public static CreateArchiveDownloadLinkResult BadRequest(string error) => new(null, error, StatusCodes.Status400BadRequest);
        /// <summary>
        /// Creates a not-found result.
        /// </summary>
        public static CreateArchiveDownloadLinkResult NotFound(string error) => new(null, error, StatusCodes.Status404NotFound);
    }
}
