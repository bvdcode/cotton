// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Cotton.Server.Auth
{
    public class DatabaseBackupAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DatabaseBackupTokenService tokens) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "DatabaseBackup";
        public const string HeaderName = "X-Cotton-Backup-Token";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string token = Request.Headers[HeaderName].ToString();
            if (string.IsNullOrEmpty(token))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }
            if (!tokens.Validate(token))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid database backup token."));
            }
            ClaimsIdentity identity = new([new Claim(ClaimTypes.Name, "database-backup")], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
