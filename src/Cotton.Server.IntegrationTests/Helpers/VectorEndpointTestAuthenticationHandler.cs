// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class VectorEndpointTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "VectorEndpointTest";
        public const string RoleHeader = "X-Test-Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string role = Request.Headers[RoleHeader].ToString();
            if (string.IsNullOrEmpty(role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            ClaimsIdentity identity = new([new Claim(ClaimTypes.Role, role)], SchemeName);
            AuthenticationTicket ticket = new(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
