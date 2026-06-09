// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Progress
{
    /// <summary>
    /// Publishes live transfer progress to in-process subscribers.
    /// </summary>
    public interface IAppTransferProgressPublisher : IObservable<AppTransferProgress>
    {
        /// <summary>
        /// Publishes a transfer progress sample.
        /// </summary>
        void Publish(AppTransferProgress progress);
    }
}
