// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Text.RegularExpressions;

namespace Cotton.Server.Services
{
    public record LinuxContainerSecuritySnapshot(
        bool? RootFilesystemReadOnly,
        bool DockerSocketMounted,
        bool? HostPidNamespaceLikely,
        string? ProcOneCommandLine,
        string? CoreDumpSoftLimit,
        string? CoreDumpHardLimit,
        bool? CoreDumpSoftLimitDisabled,
        string? CorePattern,
        string? AppArmorProfile,
        string? SelinuxContext,
        bool? SelinuxEnforcing);
}
