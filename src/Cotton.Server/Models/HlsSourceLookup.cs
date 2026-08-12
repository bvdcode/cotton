// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Models
{
    internal record HlsSourceLookup(
        NodeFile? NodeFile,
        DownloadToken? DownloadToken,
        IActionResult? Failure);
}
