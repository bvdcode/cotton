// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;

namespace Cotton.Sync.Desktop.Diagnostics;

internal static class DesktopUnhandledExceptionReporter
{
    private static int s_isInstalled;

    public static void Install()
    {
        if (Interlocked.Exchange(ref s_isInstalled, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    internal static string FormatUnhandledException(object? exceptionObject, bool isTerminating)
    {
        string details = exceptionObject switch
        {
            Exception exception => exception.ToString(),
            null => "No exception object was provided.",
            _ => exceptionObject.ToString() ?? exceptionObject.GetType().FullName ?? "Unknown exception object.",
        };
        return DesktopSecretRedactor.Redact(
            "Unhandled desktop exception captured. Terminating: "
            + isTerminating
            + Environment.NewLine
            + details);
    }

    internal static string FormatUnobservedTaskException(AggregateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return DesktopSecretRedactor.Redact(
            "Unobserved desktop task exception captured."
            + Environment.NewLine
            + exception);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        Trace.TraceError(FormatUnhandledException(args.ExceptionObject, args.IsTerminating));
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        Trace.TraceError(FormatUnobservedTaskException(args.Exception));
    }
}
