# 16. Real-Time Events, Notifications, and Email

Cotton has three related communication channels with different durability:

- SignalR events update connected clients immediately;
- database notifications provide an in-app history;
- email delivers selected account and security events outside the application.

A producer may use more than one channel, but each channel keeps its own delivery semantics.

## Real-time events

Authenticated clients connect to the event hub with the normal application identity. The server targets events to the affected user rather than broadcasting private filesystem data globally.

Events describe completed changes such as file or folder creation, update, movement, deletion, restoration, session revocation, or notification creation. The database mutation commits before the event is sent. A SignalR delivery failure does not roll back committed state; clients reconcile by refetching authoritative data.

Event payloads are public compatibility contracts. Internal entity graphs are not serialized directly.

## In-app notifications

Notifications are persisted per user with a type, localized template key or display content, read state, and timestamps. Creating a notification stores it first and then emits a real-time event so connected clients can update their unread state.

Common producers include:

- security-sensitive account changes;
- missing or mismatched stored content;
- shared-file download activity;
- background diagnostics requiring user attention.

Repeated high-frequency events may be coalesced or debounced when individual records would provide no additional value.

## Email modes

Email delivery is configured as one of these modes:

- disabled;
- a managed relay mode;
- direct SMTP using administrator-provided settings.

The selected mode determines transport only. Password reset, verification, sign-in alerts, password changes, external-identity changes, and similar producers use the same email abstraction.

SMTP credentials are encrypted at rest and never returned in plaintext. Saving email configuration and sending a test message are distinct operations so administrators can verify deliverability deliberately.

## Shared template system

Transactional email uses one renderer and shared visual shell. The current message variants are email confirmation, password reset, and a generic security alert. Structured inputs include:

- subject and heading;
- primary message;
- optional action label and URL;
- optional contextual details;
- standard instance identity and footer.

Individual security events provide title, message, context, and optional action data to the generic security-alert variant instead of adding one HTML document per event. Confirmation and password reset retain their purpose-specific bodies while sharing the same header, footer, branding, and transport path.

The renderer selects an available language variant and falls back to English when that variant is missing. Action URLs must be derived from the configured or trusted public base URL, and callers must supply values safe for HTML substitution.

## Security email behavior

Security messages are sent after the protected change succeeds. Typical events include successful login, password change or reset, TOTP changes, session revocation, and OIDC link changes.

Delivery failure is logged but normally does not reverse the security mutation. Messages must not contain passwords, master keys, refresh tokens, provider secrets, or other reusable credentials.

## Failure and consistency

- SignalR is immediate but not durable; reconnecting clients refetch.
- In-app notifications are durable but may be delivered to the UI after a short delay.
- Email depends on an external transport and is best-effort unless a specific workflow explicitly requires successful delivery before continuing.
- Notification fan-out must not mark an integrity alert complete until all required durable notification records are created.
- Deleting a user removes or invalidates that user's channel state according to normal ownership rules.

## Related sections

See [Authentication and Sessions](13-authentication-sessions.md), [Passkeys and OIDC](14-passkeys-oidc.md), [Frontend Architecture](23-frontend-architecture.md), and [Configuration and Startup](25-configuration-startup.md).
