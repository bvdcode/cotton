// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    public class AdminChangeUserPasswordRequestDto
    {
        public string Password { get; set; } = null!;
    }
}
