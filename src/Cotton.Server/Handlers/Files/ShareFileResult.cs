// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Handlers.Files
{
    public record ShareFileResult(IActionResult Response, bool IsTokenLookupFailure = false);
}
