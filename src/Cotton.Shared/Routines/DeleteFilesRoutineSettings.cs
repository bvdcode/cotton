// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;

namespace Cotton.Routines
{
    public class DeleteFilesRoutineSettings
    {
        [Range(1, 36500)]
        public int OlderThanDays { get; set; }

        [EnumDataType(typeof(RoutineDateField))]
        public RoutineDateField DateField { get; set; }

        public bool IncludeSubfolders { get; set; }

        public bool PermanentlyDelete { get; set; }
    }
}
