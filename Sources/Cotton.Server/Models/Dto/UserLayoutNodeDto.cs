// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class UserLayoutNodeDto : BaseDto<Guid>
    {
        public Guid LayoutId { get; set; }
        public Guid? ParentId { get; set; }
        public string Name { get; set; } = null!;
    }
}
