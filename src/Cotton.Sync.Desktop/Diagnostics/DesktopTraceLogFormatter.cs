// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Cotton.Sync.Desktop.Diagnostics
{
    internal static class DesktopTraceLogFormatter
    {
        public static string Format(
            string categoryName,
            LogLevel logLevel,
            EventId eventId,
            string message,
            Exception? exception)
        {
            string eventText = eventId.Id == 0 && string.IsNullOrWhiteSpace(eventId.Name)
                ? string.Empty
                : " event=" + FormatEvent(eventId);
            string formatted = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)
                + " "
                + logLevel
                + " ["
                + categoryName
                + "]"
                + eventText
                + " "
                + message;
            return exception is null ? formatted : formatted + Environment.NewLine + exception;
        }

        private static string FormatEvent(EventId eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId.Name))
            {
                return eventId.Id.ToString(CultureInfo.InvariantCulture);
            }

            return eventId.Id.ToString(CultureInfo.InvariantCulture) + ":" + eventId.Name;
        }
    }
}
