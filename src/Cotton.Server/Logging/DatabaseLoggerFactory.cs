// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Logging
{
    internal class DatabaseLoggerFactory(ILoggerFactory inner) : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName)
        {
            ILogger logger = inner.CreateLogger(categoryName);
            return categoryName == DbLoggerCategory.Database.Command.Name || categoryName == DbLoggerCategory.Update.Name
                ? new DatabaseLogger(logger)
                : logger;
        }

        public void AddProvider(ILoggerProvider provider) => inner.AddProvider(provider);

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
