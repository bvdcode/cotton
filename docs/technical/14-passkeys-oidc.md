# 14. Passkeys and OIDC

Passkeys and OpenID Connect provide alternative ways to establish the standard Cotton session. Neither mechanism bypasses account state, role, session, or database-integrity checks.

## Passkeys

Passkey registration is an authenticated WebAuthn ceremony:

1. The server creates registration options bound to the current user and relying party.
2. The browser asks the authenticator to create a credential.
3. The server verifies the challenge, origin, relying-party identifier, attestation response, and user binding.
4. The credential is persisted only after verification succeeds.

Passkey sign-in follows the corresponding assertion ceremony. The challenge is short-lived and single-purpose. The server verifies the signature and advances the authenticator counter according to WebAuthn rules before issuing a Cotton session.

Users can list, label, and remove their credentials, and removal generates a security email when delivery is available. The current passkey-delete path does not perform the same last-sign-in-method check as OIDC unlinking; users must retain another credential or a usable email recovery path.

The relying-party origin and identifier must match the public URL seen by browsers. Reverse-proxy deployments therefore require correct public-base and forwarded-protocol configuration.

## OIDC provider configuration

Administrators configure an issuer, client identifier, client secret, requested scopes, account-creation policy, default role, allowed email domains, and profile synchronization behavior.

Before a provider configuration is saved, the server performs discovery and validates that the issuer exposes the endpoints and signing metadata required for the configured flow. A missing or invalid discovery document is a validation failure, not a reason to guess provider endpoints.

Client secrets are encrypted at rest. Administrative responses never return the decrypted secret.

## Authorization flow

Cotton uses authorization code flow with PKCE, state, and nonce:

1. The server creates an expiring login-state record and returns the provider authorization URL.
2. The provider redirects to `/api/v1/auth/oidc/callback` with a code and state.
3. Cotton consumes the state once, exchanges the code, validates the ID token issuer, audience, signature, lifetime, and nonce, and optionally loads user-info claims.
4. The resulting external identity is resolved or created according to provider policy.
5. Cotton issues its normal access and refresh session.

The durable external identity key is `(provider, subject)`. Email is profile data and account-creation input; it is not used as an automatic account-link credential.

## Sign-in, linking, and account creation

An existing `(provider, subject)` link signs in its bound user and may synchronize allowed profile fields.

If no link exists:

- an email matching an existing Cotton account does not auto-link; the user must authenticate normally and start the explicit linking flow;
- a new account may be created only when the provider policy allows the supplied claims;
- verified-email and allowed-domain requirements are enforced before creation;
- the provider's configured default role is assigned at creation and is not continuously replaced by an arbitrary `groups` claim.

Explicit linking starts from an authenticated Cotton session. The callback binds the verified provider subject to that user. A provider subject cannot be linked to two users, and one user's existing link for a provider cannot silently change to a different subject.

This avoids account takeover through recycled or unverified email addresses and avoids making application authorization depend on an unstable external group mapping.

## Profile synchronization

Provider display name, given name, family name, verified email, and avatar may be synchronized according to configured policy. Avatar import accepts only supported secure URLs and is best-effort; failure does not invalidate a successful authentication.

Role changes remain an explicit Cotton administrative decision after account creation. If external role synchronization is introduced later, it requires a defined mapping, removal policy, outage behavior, and audit trail rather than implicit claim copying.

## Failure behavior

- Login state, PKCE verifier, and nonce expire and are single-use.
- Discovery, token exchange, signature validation, and claim-policy failures stop the flow.
- Callback errors do not create partial external-identity links.
- Unlinking is rejected if it would remove the user's last viable sign-in or recovery path.
- Linking and unlinking generate security notifications after persistence.

## Related sections

See [Authentication and Sessions](13-authentication-sessions.md), [Real-Time Events, Notifications, and Email](16-realtime-notifications-email.md), [Database Integrity](20-database-integrity.md), and [Deployment and Operations](27-deployment-operations.md).
