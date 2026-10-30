// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.Passkeys;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class FailingPasskeyClient(Exception error) : IFido2, IPasskeyClientFactory
    {
        public Task<IFido2> CreateAsync(CancellationToken cancellationToken) => Task.FromResult<IFido2>(this);
        public CredentialCreateOptions RequestNewCredential(RequestNewCredentialParams request) => throw new NotSupportedException();
        public AssertionOptions GetAssertionOptions(GetAssertionOptionsParams request) => throw new NotSupportedException();
        public Task<RegisteredPublicKeyCredential> MakeNewCredentialAsync(MakeNewCredentialParams request, CancellationToken cancellationToken = default)
            => Task.FromException<RegisteredPublicKeyCredential>(error);
        public Task<VerifyAssertionResult> MakeAssertionAsync(MakeAssertionParams request, CancellationToken cancellationToken = default)
            => Task.FromException<VerifyAssertionResult>(error);
    }
}
