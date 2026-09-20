// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.TextExtraction
{
    public class FileTextExtractionException(string message, Exception innerException) : Exception(message, innerException)
    {
    }
}
