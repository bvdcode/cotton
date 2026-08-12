// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Quartz.Logging;

namespace Cotton.Server.IntegrationTests.Common
{
    internal class NoOpQuartzLogProvider : ILogProvider, IDisposable
    {
        private static readonly Logger NoOpLogger = (_, _, _, _) => false;

        public Logger GetLogger(string name)
        {
            return NoOpLogger;
        }

        public IDisposable OpenNestedContext(string message)
        {
            return this;
        }

        public IDisposable OpenMappedContext(string key, object value, bool destructure = false)
        {
            return this;
        }

        public void Dispose()
        {
        }
    }
}
