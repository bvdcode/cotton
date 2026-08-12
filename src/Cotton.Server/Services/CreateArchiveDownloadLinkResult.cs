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
    public class CreateArchiveDownloadLinkResult
    {
        private CreateArchiveDownloadLinkResult(ArchiveDownloadLinkDto? link, string? error, int statusCode)
        {
            Link = link;
            Error = error;
            StatusCode = statusCode;
        }

        public ArchiveDownloadLinkDto? Link { get; }

        public string? Error { get; }

        public int StatusCode { get; }

        public static CreateArchiveDownloadLinkResult Success(ArchiveDownloadLinkDto link) => new(link, null, StatusCodes.Status200OK);

        public static CreateArchiveDownloadLinkResult BadRequest(string error) => new(null, error, StatusCodes.Status400BadRequest);

        public static CreateArchiveDownloadLinkResult NotFound(string error) => new(null, error, StatusCodes.Status404NotFound);
    }
}
