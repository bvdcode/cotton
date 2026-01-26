// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class NodeDto : BaseDto<Guid>
    {
        public Guid LayoutId { get; set; }
        public Guid? ParentId { get; set; }
        public string Name { get; set; } = null!;
    }
}
