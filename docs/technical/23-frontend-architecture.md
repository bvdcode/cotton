# 23. Frontend Architecture

The web client is a React and TypeScript single-page application built with Vite. It consumes the versioned HTTP API and SignalR event hub; it does not reach into server persistence or duplicate server authorization rules.

## Application boundaries

The frontend is organized around these responsibilities:

- application bootstrap, routing, providers, and top-level layouts;
- shared typed API transport and schemas;
- server-state queries and mutations;
- small UI and identity stores;
- reusable design-system components;
- feature pages composed from focused hooks and views;
- optional browser-side encryption.

Pages coordinate a feature but should not accumulate transport, cryptography, domain mutation, and presentation logic in one component. Large features are split by cohesive behavior rather than by moving arbitrary blocks into files with the same dependencies.

## State ownership

TanStack Query owns remote server state: loading, caching, invalidation, pagination, and mutation refresh. Zustand owns client-only state such as selection, view preferences, active task presentation, authentication UI state, and the in-memory encryption vault.

A value has one authoritative owner. Components receive focused values or feature objects rather than a broad bag of unrelated state. Derived display state is computed from the owner instead of copied into a second store.

Persistent browser state is accessed only through established store adapters. Components do not call browser storage directly.

## HTTP transport

One configured HTTP client provides:

- the `/api/v1` base path;
- credentials and common request headers;
- trusted timezone metadata;
- access-token refresh coordination;
- cancellation support;
- consistent error extraction;
- response validation where schemas are defined.

Concurrent `401` responses share one refresh attempt so the browser does not create a refresh storm. A failed refresh clears authenticated client state and routes the user through the normal sign-in flow.

Paged API methods return both payload and the required `X-Total-Count` value. Missing or invalid pagination headers are contract errors rather than an invitation to infer totals from page length.

User-entered text is trimmed before submission. API modules expose typed feature operations; components do not assemble ad hoc URLs or inspect transport-specific error shapes.

## Real-time updates

SignalR events arrive after server commits. The client either patches a narrowly identified cached value or invalidates the relevant query family. Bursts are coalesced when a refetch is cheaper and safer than applying many partial events.

Real-time state is an optimization, not a second source of truth. Reconnect, missed events, or a new tab must converge through normal API reads.

## Components and responsive layout

MUI supplies the visual primitives and theme. Components rely on theme values and built-in state styling rather than raw colors or duplicated CSS.

Feature containers separate data and actions from presentation. Reusable components receive domain-focused props, while hooks expose cohesive controllers rather than dozens of unrelated parameters.

Every changed layout is checked at desktop and mobile widths. Mobile toolbars may reorder content and distribute primary actions across available width, but the same actions and accessibility labels remain available. Navigation uses real links where browser link behavior is expected.

## Internationalization and accessibility

All visible strings use i18n keys. Locale completeness is checked by the repository script. Icon-only controls require accessible names, selected navigation exposes its current state, and dialogs manage focus and keyboard actions through established MUI behavior.

## Browser-side encryption

Optional client-side encryption is separate from server at-rest encryption. The browser vault holds the client master key and derives content and display-metadata keys without sending the plaintext master key to the server.

Encrypted files carry the metadata required for the browser to recognize and decrypt them. Folder policy controls whether new or moved content should be encrypted, but changing policy does not silently claim that all existing descendants were transformed; explicit background tasks report successes, failures, and incomplete scans.

Sharing actions that the server cannot safely perform on client-encrypted content are hidden or rejected explicitly.

## Verification

Frontend changes are expected to pass:

```text
npm run lint
npm run test
npm run i18n:check
npm run build
```

Feature behavior should be tested at the smallest stable boundary: pure model functions, hooks, components, or API modules. Tests should assert user-visible behavior and public contracts rather than private component structure.

## Related sections

See [Frontend Features and Upload Pipeline](24-frontend-features-upload.md), [HTTP API and Mediator Layer](12-http-api-mediator.md), and [Real-Time Events, Notifications, and Email](16-realtime-notifications-email.md).
