# OpenID Connect (OIDC) setup

Cotton can use OpenID Connect providers for sign-in and account linking. This guide describes what Cotton expects from an OIDC provider and the values that should be registered on both sides.

## Callback URL

Register this redirect/callback URL in the provider application:

```text
https://your-cotton-domain/api/v1/auth/oidc/callback
```

The callback path is the same for every provider. The provider slug is not part of the callback URL.

In Cotton, the same URL is shown in the identity provider form as the redirect URL. Copy it exactly when creating the provider application.

## Public base URL

Cotton builds OIDC callbacks from the configured public base URL. For reverse-proxy or container deployments, make sure the public base URL is the externally reachable HTTPS origin, for example:

```text
https://cloud.example.com
```

If Cotton is behind a reverse proxy, the proxy should preserve the external scheme and host with forwarded headers such as `X-Forwarded-Proto` and `X-Forwarded-Host`.

## Cotton provider fields

| Field | Meaning |
| --- | --- |
| Name | Display name shown on the login page. |
| Slug | Stable provider identifier in Cotton URLs. Leave empty to generate it from the name. |
| Issuer URL | The OIDC issuer URL. Do not include `/.well-known/openid-configuration`. |
| Client ID | Client/application ID from the provider. |
| Client secret | Client/application secret. Leave empty only for providers configured as public clients. |
| Scopes | Use `openid profile email` unless your provider requires a different set. Cotton always requires `openid`. |
| Enable sign-in | Shows this provider on the login page. |
| Allow account creation | Allows first-time OIDC users to get a Cotton account automatically. |
| Require verified email | Blocks auto-created accounts unless the provider sends `email_verified=true`. |
| Allowed email domains | Optional comma-separated allow-list for automatic account creation. |
| Default role | Role assigned to auto-created accounts. Admin is intentionally not allowed as a default role. |
| Sync profile names | Updates first/last name from provider claims on sign-in. |
| Import avatar | Imports the provider avatar when the Cotton user has no avatar yet. |

## Claims used by Cotton

Cotton requires a stable `sub` claim. The following claims are optional but improve the user profile and account creation flow:

- `email`
- `email_verified`
- `name`
- `given_name`
- `family_name`
- `preferred_username`
- `picture`

## Authentik example

In Authentik, create an OAuth2/OpenID provider/application for Cotton.

Use these values as a starting point:

| Authentik setting | Value |
| --- | --- |
| Redirect URI | `https://your-cotton-domain/api/v1/auth/oidc/callback` |
| Client type | Confidential, unless you intentionally use a public client. |
| Grant type | Authorization code. |
| Scopes | `openid profile email` |
| Issuer URL in Cotton | The Authentik provider issuer, usually the provider URL without `/.well-known/openid-configuration`. |

After saving the Authentik provider, copy the client ID and client secret into Cotton.

## Troubleshooting

If the provider accepts the login but Cotton returns to the login page:

- Check that the browser receives a `refresh_token` cookie from Cotton after returning from the provider.
- Check that `POST /api/v1/auth/refresh` succeeds after the callback.
- Confirm that Cotton public base URL exactly matches the external HTTPS URL users open in the browser.
- Confirm that the provider redirect URI exactly matches Cotton's callback URL.
- If running behind a reverse proxy, confirm forwarded host/proto headers are passed through.
- Check whether browser privacy settings or embedded contexts block cookies for the Cotton domain.

If the user is created but cannot sign in again, confirm that the provider sends the same stable `sub` claim for every login and that the provider remains enabled in Cotton.
