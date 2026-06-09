// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.Desktop.Shell
{

    internal sealed class SyncPairValidationException : Exception
    {
        public SyncPairValidationException(IReadOnlyList<SyncPairValidationError> errors)
            : base(CreateMessage(errors))
        {
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        public IReadOnlyList<SyncPairValidationError> Errors { get; }

        private static string CreateMessage(IReadOnlyList<SyncPairValidationError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            return errors.Count == 0
                ? "Sync pair validation failed."
                : string.Join(Environment.NewLine, errors.Select(static error => error.Message));
        }
    }
}
