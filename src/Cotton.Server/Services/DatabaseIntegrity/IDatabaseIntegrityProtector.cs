// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity
{
    public interface IDatabaseIntegrityProtector
    {
        /// <summary>
        /// Signs using the latest schema, including when the supplied descriptor selects an older version.
        /// </summary>
        byte[] Sign(object entity, IDatabaseIntegrityDescriptor descriptor);

        /// <summary>
        /// Verifies using the schema version selected by the supplied descriptor.
        /// </summary>
        bool Verify(object entity, IDatabaseIntegrityDescriptor descriptor, byte[] expectedMac);

        void RequireValid(object entity, IDatabaseIntegrityDescriptor descriptor, byte[] expectedMac);
    }
}
