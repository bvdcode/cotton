// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

namespace Cotton.Server.Models.Requests
{
    public class CreateNodeRequest
    {
        public Guid ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
