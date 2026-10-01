// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Exceptions;

namespace Cotton.Server.Services
{
    public static class ListingRequestLimits
    {
        public const int MaxPageSize = 1000;
        public const int MaxRecentCount = 1000;
        public const int MaxDepth = 32;
        public const int MaxTraversedNodes = 50_000;

        public static int GetSkip(int page, int pageSize)
        {
            if (page <= 0 || pageSize <= 0 || pageSize > MaxPageSize)
            {
                throw new BadRequestException($"Page must be positive and page size must be between 1 and {MaxPageSize}.");
            }

            long skip = ((long)page - 1) * pageSize;
            if (skip > int.MaxValue)
            {
                throw new BadRequestException("Requested page is too far from the beginning of the list.");
            }

            return (int)skip;
        }

        public static void ValidateRecentCount(int count)
        {
            if (count <= 0 || count > MaxRecentCount)
            {
                throw new BadRequestException($"Recent file count must be between 1 and {MaxRecentCount}.");
            }
        }

        public static void ValidateDepth(int depth)
        {
            if (depth < 0 || depth > MaxDepth)
            {
                throw new BadRequestException($"Folder depth must be between 0 and {MaxDepth}.");
            }
        }
    }
}
