// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Contracts.Common
{
    /// <summary>
    /// Represents the common API DTO fields returned by Cotton resources.
    /// </summary>
    public class BaseApiDto : BaseApiDto<Guid>
    {
    }

    /// <summary>
    /// Represents the common API DTO fields returned by Cotton resources with a typed identifier.
    /// </summary>
    /// <typeparam name="TId">The DTO identifier type.</typeparam>
    public class BaseApiDto<TId>
        where TId : struct
    {
        /// <summary>
        /// Gets or sets the resource identifier.
        /// </summary>
        public TId Id { get; set; }

        /// <summary>
        /// Gets or sets the UTC creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the UTC update timestamp.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}
