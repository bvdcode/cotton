// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Services.Passkeys
{
    internal record PasskeyRegistrationState(Guid UserId, string? Label, CredentialCreateOptions Options);
}
