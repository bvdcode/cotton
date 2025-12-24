// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    public class CreateNodeRequest
    {
        public Guid ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
