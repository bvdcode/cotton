// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using EasyExtensions.AspNetCore.Exceptions;

namespace Cotton.Server.Handlers.Moves
{
    public class InvalidFolderMoveException(string message) : BadRequestException<Node>(message);
}
