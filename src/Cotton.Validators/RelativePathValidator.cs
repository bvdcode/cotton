// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Validators
{
    /// <summary>
    /// Normalizes and validates cross-platform relative paths segment by segment.
    /// </summary>
    public static class RelativePathValidator
    {
        /// <summary>
        /// Normalizes a relative path and validates every segment with the shared name policy.
        /// </summary>
        public static bool TryNormalizeAndValidate(
            string input,
            out string normalized,
            out string errorMessage)
        {
            normalized = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                errorMessage = "Path cannot be empty or whitespace.";
                return false;
            }

            string path = input.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = "Path cannot be empty after trimming separators.";
                return false;
            }

            string[] segments = path.Split('/');
            var normalizedSegments = new string[segments.Length];
            for (int index = 0; index < segments.Length; index++)
            {
                string segment = segments[index];
                if (string.IsNullOrWhiteSpace(segment))
                {
                    errorMessage = $"Path segment {index + 1} cannot be empty or whitespace.";
                    return false;
                }

                if (!NameValidator.TryNormalizeAndValidate(segment, out string normalizedSegment, out string segmentError))
                {
                    errorMessage = $"Invalid path segment '{segment}': {segmentError}";
                    return false;
                }

                normalizedSegments[index] = normalizedSegment;
            }

            normalized = string.Join("/", normalizedSegments);
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Normalizes a relative path or throws when it violates the shared path policy.
        /// </summary>
        public static string NormalizeOrThrow(string relativePath)
        {
            if (TryNormalizeAndValidate(relativePath, out string normalized, out string errorMessage))
            {
                return normalized;
            }

            throw new ArgumentException($"Invalid relative path: {errorMessage}", nameof(relativePath));
        }
    }
}
