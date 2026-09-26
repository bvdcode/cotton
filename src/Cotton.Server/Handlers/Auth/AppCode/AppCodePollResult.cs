// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Authorization.Models.Dto;

namespace Cotton.Server.Handlers.Auth.AppCode
{
    public record AppCodePollResult(AppCodePollStatus Status, TokenPairResponseDto? Tokens = null);
}
