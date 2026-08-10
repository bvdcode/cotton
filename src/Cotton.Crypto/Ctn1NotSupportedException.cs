// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Crypto
{
    /// <summary>
    /// Indicates that encrypted data still uses the obsolete CTN1 container format.
    /// </summary>
    [Obsolete("OBSOLETE TRANSITION: remove this exception and CTN1 header detection after the 0.5 cutover window.")]
    public class Ctn1NotSupportedException : NotSupportedException
    {
        /// <summary>
        /// Initializes the CTN1 upgrade error.
        /// </summary>
        public Ctn1NotSupportedException()
            : base(
                "CTN1 encrypted data is not supported by this Cotton version. "
                + "Start Cotton 0.4.35 with the same database, storage, and master key, "
                + "wait for the CTN2 transition to complete, then upgrade again.")
        {
        }
    }
}
