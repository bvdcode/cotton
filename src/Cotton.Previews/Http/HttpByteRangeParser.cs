// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Net.Http.Headers;

namespace Cotton.Previews.Http
{
    internal static class HttpByteRangeParser
    {
        public static bool TryParse(
            string? value,
            long contentLength,
            out HttpByteRange? range,
            out int errorStatusCode,
            out string? contentRangeHeaderValue)
        {
            range = null;
            errorStatusCode = (int)HttpStatusCode.OK;
            contentRangeHeaderValue = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!RangeHeaderValue.TryParse(value, out RangeHeaderValue? rangeHeader)
                || !string.Equals(rangeHeader.Unit, "bytes", StringComparison.OrdinalIgnoreCase)
                || rangeHeader.Ranges.Count != 1)
            {
                errorStatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                return false;
            }

            RangeItemHeaderValue requestedRange = rangeHeader.Ranges.Single();
            if (!TryResolve(requestedRange, contentLength, out long start, out long end))
            {
                errorStatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                return false;
            }

            if (start >= contentLength)
            {
                errorStatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                contentRangeHeaderValue = $"bytes */{contentLength}";
                return false;
            }

            end = Math.Clamp(end, start, contentLength - 1);
            range = new HttpByteRange(start, end);
            return true;
        }

        private static bool TryResolve(
            RangeItemHeaderValue range,
            long contentLength,
            out long start,
            out long end)
        {
            start = 0;
            end = 0;

            if (range.From is null)
            {
                if (range.To is not long suffixLength || suffixLength <= 0)
                {
                    return false;
                }

                start = Math.Max(0, contentLength - suffixLength);
                end = contentLength - 1;
                return true;
            }

            start = range.From.Value;
            end = range.To ?? contentLength - 1;
            return true;
        }
    }
}
