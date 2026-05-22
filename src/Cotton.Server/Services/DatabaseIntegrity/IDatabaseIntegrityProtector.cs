// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.DatabaseIntegrity;

public interface IDatabaseIntegrityProtector
{
    byte[] Sign(object entity, IDatabaseIntegrityDescriptor descriptor);
    bool Verify(object entity, IDatabaseIntegrityDescriptor descriptor, byte[] expectedMac);
    void RequireValid(object entity, IDatabaseIntegrityDescriptor descriptor, byte[] expectedMac);
}
