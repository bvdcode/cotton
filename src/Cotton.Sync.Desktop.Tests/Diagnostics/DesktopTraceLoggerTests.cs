// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using Cotton.Sync.Desktop.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Cotton.Sync.Desktop.Tests.Diagnostics;

public sealed class DesktopTraceLoggerTests
{
    [Test]
    public void Format_IncludesLevelCategoryEventAndException()
    {
        var exception = new InvalidOperationException("sync failed");

        string formatted = DesktopTraceLogFormatter.Format(
            "Cotton.Sync.App.Runners.SyncPairRunner",
            LogLevel.Warning,
            new EventId(42, "Retry"),
            "Retrying sync",
            exception);

        Assert.Multiple(() =>
        {
            Assert.That(formatted, Does.Contain("Warning"));
            Assert.That(formatted, Does.Contain("[Cotton.Sync.App.Runners.SyncPairRunner]"));
            Assert.That(formatted, Does.Contain("event=42:Retry"));
            Assert.That(formatted, Does.Contain("Retrying sync"));
            Assert.That(formatted, Does.Contain("sync failed"));
        });
    }

    [Test]
    public void Log_WritesRedactedMessageToTrace()
    {
        var listener = new CollectingTraceListener();
        Trace.Listeners.Add(listener);
        try
        {
            ILogger logger = new DesktopTraceLogger("Cotton.Sync.Desktop.Tests");

            logger.LogError("Authorization: Bearer access-token");

            Assert.Multiple(() =>
            {
                Assert.That(listener.Output, Does.Contain("Error"));
                Assert.That(listener.Output, Does.Contain("Cotton.Sync.Desktop.Tests"));
                Assert.That(listener.Output, Does.Contain("Bearer [redacted]"));
                Assert.That(listener.Output, Does.Not.Contain("access-token"));
            });
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    private sealed class CollectingTraceListener : TraceListener
    {
        public string Output { get; private set; } = string.Empty;

        public override void Write(string? message)
        {
            Output += message;
        }

        public override void WriteLine(string? message)
        {
            Output += message + Environment.NewLine;
        }
    }
}
