// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

namespace Cotton.Server.Models
{
    public record LoginResponse(string AccessToken, string RefreshToken);
}
