// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public class CommittedMutationEffects(ILogger<CommittedMutationEffects> logger)
    {
        public async Task RunAsync(string operation, Func<Task> effect)
        {
            try
            {
                await effect();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Post-commit operation {Operation} failed", operation);
            }
        }
    }
}
