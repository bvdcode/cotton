// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Diagnostics;

namespace Cotton.Sync.Desktop.Tests.Diagnostics
{
    public sealed class DesktopUnhandledExceptionReporterTests
    {
        [Test]
        public void FormatUnhandledException_RedactsSecretsAndMarksTerminatingState()
        {
            var exception = new InvalidOperationException(
                "Failed with Authorization: Bearer secret-token and password=secret-password");

            string message = DesktopUnhandledExceptionReporter.FormatUnhandledException(exception, isTerminating: true);

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("Unhandled desktop exception captured."));
                Assert.That(message, Does.Contain("Terminating: True"));
                Assert.That(message, Does.Contain(nameof(InvalidOperationException)));
                Assert.That(message, Does.Contain("Bearer [redacted]"));
                Assert.That(message, Does.Contain("password=[redacted]"));
                Assert.That(message, Does.Not.Contain("secret-token"));
                Assert.That(message, Does.Not.Contain("secret-password"));
            });
        }

        [Test]
        public void FormatUnobservedTaskException_RedactsSecretsAndKeepsExceptionContext()
        {
            var exception = new AggregateException(
                new InvalidOperationException("""{"accessToken":"secret-token","message":"failed"}"""));

            string message = DesktopUnhandledExceptionReporter.FormatUnobservedTaskException(exception);

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("Unobserved desktop task exception captured."));
                Assert.That(message, Does.Contain(nameof(AggregateException)));
                Assert.That(message, Does.Contain("\"accessToken\":\"[redacted]\""));
                Assert.That(message, Does.Not.Contain("secret-token"));
            });
        }
    }
}
