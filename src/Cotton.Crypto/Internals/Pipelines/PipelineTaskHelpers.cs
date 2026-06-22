// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Cotton.Crypto.Internals.Pipelines
{
    internal static class PipelineTaskHelpers
    {
        public static void CancelOnFailure(Task task, CancellationTokenSource cts)
        {
            _ = task.ContinueWith(static (completed, state) =>
            {
                if (!completed.IsFaulted && !completed.IsCanceled)
                {
                    return;
                }

                try
                {
                    ((CancellationTokenSource)state!).Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The owner already observed and disposed the pipeline CTS.
                }
            }, cts, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public static async Task CompleteWhenFinishedAsync<T>(Task task, ChannelWriter<T> writer)
        {
            try
            {
                await task.ConfigureAwait(false);
                writer.TryComplete();
            }
            catch (Exception ex)
            {
                writer.TryComplete(ex);
                throw;
            }
        }

        public static async Task ObserveAllAsync(params Task[] tasks)
        {
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort observation during failure cleanup. The original exception is rethrown by the caller.
            }
        }
    }
}
