// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.Runners
{
    /// <summary>
    /// Creates sync pair runners using a shared sync-work adapter.
    /// </summary>
    public sealed class SyncPairRunnerFactory : ISyncPairRunnerFactory
    {
        private readonly SyncPairRunnerRetryOptions _retryOptions;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ISyncPairWork _work;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncPairRunnerFactory" /> class.
        /// </summary>
        public SyncPairRunnerFactory(
            ISyncPairWork work,
            SyncPairRunnerRetryOptions? retryOptions = null,
            ILoggerFactory? loggerFactory = null)
        {
            _work = work ?? throw new ArgumentNullException(nameof(work));
            _retryOptions = (retryOptions ?? SyncPairRunnerRetryOptions.Default).Normalize();
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        }

        /// <inheritdoc />
        public ISyncPairRunner Create(SyncPairSettings syncPair)
        {
            return new SyncPairRunner(
                syncPair,
                _work,
                _retryOptions,
                _loggerFactory.CreateLogger<SyncPairRunner>());
        }
    }
}
