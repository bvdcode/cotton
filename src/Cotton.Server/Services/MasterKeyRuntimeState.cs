// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    /// <summary>
    /// Represents master key runtime state.
    /// </summary>
    public record MasterKeyRuntimeState(
        string Source,
        bool EnvironmentVariableWasConfigured,
        bool EnvironmentVariablePresentAfterResolution)
    {
        /// <summary>
        /// Creates runtime state for an interactively unlocked master key.
        /// </summary>
        public static MasterKeyRuntimeState FromUnlock(bool environmentVariablePresentAfterResolution) => new(
            "Unlock",
            false,
            environmentVariablePresentAfterResolution);

        /// <summary>
        /// Creates runtime state for a master key supplied by environment.
        /// </summary>
        public static MasterKeyRuntimeState FromEnvironment(bool environmentVariablePresentAfterResolution) => new(
            "Environment",
            true,
            environmentVariablePresentAfterResolution);
    }
}
