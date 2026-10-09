// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using EasyExtensions.Models.Dto;

namespace Cotton.Files
{
    public class BatchItemResultDto : BaseDto<Guid>
    {
        public BatchItemKind Kind { get; set; }

        public RestoreOutcomeDto? RestoreOutcome { get; set; }

        public bool Deleted { get; set; }

        public bool Failed { get; set; }
    }
}
