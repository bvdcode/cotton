// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using EasyExtensions.Models.Dto;

namespace Cotton.Files
{
    public class BatchItemRequestDto : BaseDto<Guid>
    {
        public BatchItemKind Kind { get; set; }

        public bool CreateMissingParents { get; set; }

        public bool Overwrite { get; set; }
    }
}
