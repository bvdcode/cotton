// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class SessionRestoreResponseDto : TokenPairResponseDto
    {
        public UserDto User { get; set; } = null!;
    }
}
