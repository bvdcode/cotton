// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public sealed record MasterKeyRuntimeState(
        string Source,
        bool EnvironmentVariableWasConfigured,
        bool EnvironmentVariablePresentAfterResolution)
    {
        public static MasterKeyRuntimeState FromUnlock(bool environmentVariablePresentAfterResolution) => new(
            "Unlock",
            false,
            environmentVariablePresentAfterResolution);

        public static MasterKeyRuntimeState FromEnvironment(bool environmentVariablePresentAfterResolution) => new(
            "Environment",
            true,
            environmentVariablePresentAfterResolution);
    }
}
