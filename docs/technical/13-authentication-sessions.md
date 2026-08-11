# 13. Authentication, Sessions, and Password Security

Cotton combines short-lived access tokens with database-backed refresh sessions. Password, passkey, OIDC, TOTP, and recovery flows all issue the same application session model after their own credential checks succeed.

## Session model

An access token carries the authenticated user and role for normal API authorization. A refresh token is stored as an HttpOnly cookie and corresponds to a server-side session record containing its lifetime, revocation state, and client metadata.

The server validates both the token cryptography and the current session state. Deleting or revoking a session therefore invalidates future refreshes and protected requests that require an active session.

Refresh rotation replaces the presented refresh credential rather than extending it in place. Reuse of an invalidated credential is rejected. Expired session rows are removed later by retention work but grant no access after expiry.

## Password login

Password login:

1. normalizes and validates the account identifier;
2. applies the interactive authentication limiter using the trusted client address;
3. verifies the stored password hash with the configured password-hash policy;
4. evaluates account state and TOTP requirements;
5. records relevant security metadata;
6. issues the access token and refresh session.

Authentication failures do not reveal whether an account exists. Password verification uses the stored self-describing hash parameters so policy upgrades can be handled deliberately.

## TOTP

TOTP setup creates a pending secret and requires a valid code before activation. Login accepts only the configured clock window and counts failures atomically. Reaching the failure threshold locks or rejects further attempts according to the account policy and produces a security notification.

Disabling TOTP is an authenticated security mutation and must not leave stale pending setup state.

## Password and email recovery

Forgot-password and email-verification flows use random, expiring, single-purpose tokens. The server never emails a password or reusable session credential.

Changing or resetting a password revokes sessions as required by policy and sends a security email when a deliverable verified address is available. Security notification delivery is best-effort after the account mutation; an email outage must not roll back a completed password change.

## Session management

Users can list their active sessions and revoke an individual session. Revocation is persisted first, then propagated to connected clients through the real-time channel so affected devices can leave authenticated state promptly.

Logout clears the refresh cookie even when the server-side credential is already absent or invalid. This keeps the client outcome deterministic without treating an invalid token as a valid session.

## WebDAV credentials

WebDAV uses a dedicated application token instead of the account password. Issuing or replacing that token is an authenticated operation. Revocation takes effect through the same server-side credential validation used by WebDAV Basic authentication.

## Bootstrap and public instances

A fresh private instance may permit first-administrator bootstrap only during the configured startup window and only while no administrator exists. The window is not a permanent bypass.

Public/demo account behavior is explicitly enabled by server mode and remains separate from private-instance administrator creation. Demo conveniences must not silently activate in a normal deployment.

## Abuse protection

Interactive login, refresh, OIDC start/callback paths, and other credential-sensitive endpoints have purpose-specific limits. Partition keys use the trusted client address produced by the proxy policy. Normal internal workloads such as preview generation are coordinated with semaphores or queues rather than receiving user-visible abuse responses.

## Security invariants

- Raw passwords, refresh tokens, reset tokens, and WebDAV tokens are never stored in plaintext.
- A raw forwarding header is never accepted as client identity outside the trusted-proxy boundary.
- Account lookup differences are not exposed through public failure messages.
- Security-critical changes are persisted before notifications are emitted.
- Authentication alternatives issue the same constrained application session rather than creating parallel authorization models.

## Related sections

See [Passkeys and OIDC](14-passkeys-oidc.md), [Real-Time Events, Notifications, and Email](16-realtime-notifications-email.md), [WebDAV](17-webdav.md), and [Security Hardening](22-security-hardening.md).
