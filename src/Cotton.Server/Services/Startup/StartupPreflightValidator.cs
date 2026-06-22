// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Startup
{
    internal class StartupPreflightValidator(IEnumerable<IStartupCheck> _checks) : IStartupPreflightValidator
    {
        public async Task<StartupBlocker?> ValidateAsync(CancellationToken cancellationToken)
        {
            foreach (IStartupCheck check in _checks)
            {
                StartupBlocker? blocker = await check.ValidateAsync(cancellationToken);
                if (blocker is not null)
                {
                    return blocker;
                }
            }

            return null;
        }
    }
}
