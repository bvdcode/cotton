# 22. Security Hardening and Diagnostics

Cotton combines application controls with deployment diagnostics. The security dashboard is an administrator aid, not a public health endpoint and not proof that the host is secure.

## Trusted proxy boundary

Client IP and scheme headers are trustworthy only when the immediate connection comes from the configured reverse proxy.

The proxy setting supports three modes:

- direct mode disables forwarded-address and forwarded-protocol trust;
- an exact IPv4 or IPv6 address trusts one immediate proxy;
- CIDR notation trusts an explicitly configured proxy network.

Auto-detection reports the peer that opened the current connection and suggests a configuration. A peer inside Docker's `172.16.0.0/12` private range is suggested as that network because bridge subnets may change between container recreations. Loopback and other individual peers are suggested with a host prefix such as `/32` or `/128`.

Verification saves a proxy address or network only when it contains the currently observed peer. Requests arriving from outside a configured trusted proxy boundary cannot use supplied forwarding headers.

For compatibility, an unconfigured proxy setting retains legacy header trust and is reported as a security warning. New deployments should choose direct mode or verify an explicit proxy boundary.

All consumers—including authentication limits, WebDAV limits, share lookup protection, audit metadata, and fallback public-URL construction—use the same trusted-address and trusted-protocol policy.

## Endpoint abuse protection

Rate limits are reserved for attacker-controlled credential and enumeration surfaces. Interactive authentication, refresh, OIDC, archive creation, and failed compact-share lookups use policies appropriate to their risk.

The share-lookup failure policy is enabled for compact token lengths and omitted for expanded tokens with a substantially larger search space. Availability is checked before database resolution, and only failed lookups consume the compact-token budget. Valid normal workflows are not rejected merely because the server is busy.

Internal expensive work such as preview reads, probes, and transcodes uses semaphores and bounded queues so requests wait for capacity rather than receiving an abuse response.

## Master-key protection

The root master key is read during bootstrap, validated through the normal storage graph, and removed from process and user environment variables after derivation. Runtime services receive only the derived settings they require.

Linux startup requests a non-dumpable process where supported. Deployment diagnostics warn about configurations that increase memory-extraction risk, including enabled diagnostics endpoints, core dumps, tracing capabilities, host PID sharing, a mounted container socket, writable root filesystem, or running as root.

These checks cannot protect a key from a fully privileged host administrator. They reduce accidental exposure and make dangerous container configuration visible.

## Security diagnostics

The administrator snapshot summarizes:

- public-instance posture;
- trusted-proxy mode;
- master-key source and validation state;
- administrator TOTP coverage;
- runtime diagnostics and dumpability;
- temporary-directory and container posture;
- database-integrity metadata state;
- failures while applying hardening.

Warnings have stable codes and severities so the UI can render them without parsing prose. The score is a convenience for prioritization; individual critical warnings take precedence over the aggregate number.

Diagnostics are read-only. They do not rewrite deployment configuration, repair database signatures, rotate secrets, or modify the host.

## Public URL and protocol

Security-sensitive links should use the configured public base URL. When a request-derived fallback is required, its forwarded scheme is accepted only through the trusted-proxy boundary. Raw `X-Forwarded-Proto` is never read as authority from an arbitrary client connection.

## Logging and secret handling

- Secrets and decrypted content are not logged.
- Authentication failures log enough context for diagnosis without revealing credentials.
- External-process failures may include tool diagnostics but not injected secret arguments.
- Integrity failures identify the protected entity and boundary without printing the signed payload.
- Administrative diagnostics remain authorization-protected because they reveal deployment posture.

## Related sections

See [Master Key and Unlock Bootstrap](08-master-key-bootstrap.md), [Authentication and Sessions](13-authentication-sessions.md), [Database Integrity](20-database-integrity.md), and [Deployment and Operations](27-deployment-operations.md).
