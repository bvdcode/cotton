// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.Passkeys;
using EasyExtensions.Passkeys;
using EasyExtensions.Passkeys.Models;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class FailingPasskeyServer(Exception error) : IPasskeyServer, IPasskeyServerFactory
    {
        public Task<IPasskeyServer> CreateAsync(CancellationToken cancellationToken) => Task.FromResult<IPasskeyServer>(this);
        public RegistrationOptions CreateRegistrationOptions(PasskeyUser user, IReadOnlyList<CredentialDescriptor>? excludeCredentials = null)
            => throw new NotSupportedException();
        public AuthenticationOptions CreateAuthenticationOptions(IReadOnlyList<CredentialDescriptor>? allowCredentials = null)
            => throw new NotSupportedException();
        public RegistrationResult VerifyRegistration(RegistrationResponse response, RegistrationOptions originalOptions) => throw error;
        public AuthenticationResult VerifyAuthentication(AuthenticationResponse response, AuthenticationOptions originalOptions, StoredCredential credential)
            => throw error;
    }
}
