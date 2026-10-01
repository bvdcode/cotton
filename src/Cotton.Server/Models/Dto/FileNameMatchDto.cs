// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class FileNameMatchDto : BaseDto<Guid>
    {
        public string Name { get; set; } = string.Empty;
    }
}
