// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class ShareTokenRaceInterceptor : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _readersReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _reads;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT EXISTS", StringComparison.Ordinal)
                && command.CommandText.Contains("node_share_tokens", StringComparison.Ordinal))
            {
                int reads = Interlocked.Increment(ref _reads);
                if (reads == 2)
                {
                    _readersReady.TrySetResult();
                }
                if (reads <= 2)
                {
                    await _readersReady.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                }
            }
            return result;
        }
    }
}
