// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class LinuxContainerSecurityDto
    {
        public bool? RootFilesystemReadOnly { get; init; }

        public bool DockerSocketMounted { get; init; }

        public bool? HostPidNamespaceLikely { get; init; }

        public string? ProcOneCommandLine { get; init; }

        public string? CoreDumpSoftLimit { get; init; }

        public string? CoreDumpHardLimit { get; init; }

        public bool? CoreDumpSoftLimitDisabled { get; init; }

        public string? CorePattern { get; init; }

        public string? AppArmorProfile { get; init; }

        public string? SelinuxContext { get; init; }

        public bool? SelinuxEnforcing { get; init; }
    }
}
