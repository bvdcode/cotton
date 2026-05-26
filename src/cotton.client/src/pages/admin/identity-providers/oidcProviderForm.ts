import { UserRole } from "@features/auth/types";
import type { OidcProviderDto, OidcProviderRequestDto } from "@shared/api/oidcApi";

export interface OidcProviderFormState {
  name: string;
  slug: string;
  issuer: string;
  clientId: string;
  clientSecret: string;
  clearClientSecret: boolean;
  scopes: string;
  allowedEmailDomains: string;
  isEnabled: boolean;
  allowAccountCreation: boolean;
  requireVerifiedEmail: boolean;
  defaultRole: UserRole;
  syncProfile: boolean;
  syncAvatar: boolean;
}

const OIDC_PROVIDER_SLUG_MAX_LENGTH = 64;
const DEFAULT_PUBLIC_BASE_URL = "http://localhost";

export const slugifyOidcProviderName = (value: string): string => {
  const normalized = value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9._-]+/gu, "-")
    .replace(/^-+|-+$/gu, "");
  const slug =
    normalized.length === 0 || !/^[a-z]/u.test(normalized)
      ? `oidc-${normalized}`
      : normalized;

  return slug.slice(0, OIDC_PROVIDER_SLUG_MAX_LENGTH);
};

export const resolveOidcProviderCallbackSlug = (
  state: Pick<OidcProviderFormState, "name" | "slug">,
): string => {
  const existingSlug = state.slug.trim();
  return existingSlug.length > 0
    ? existingSlug.toLowerCase()
    : slugifyOidcProviderName(state.name);
};

const trimTrailingSlash = (value: string): string => value.replace(/\/+$/u, "");

export const resolveOidcCallbackBaseUrl = (
  configuredBaseUrl: string | null | undefined,
  browserOrigin: string,
): string => {
  const configured = configuredBaseUrl?.trim();
  if (
    configured &&
    configured.length > 0 &&
    configured.toLowerCase() !== DEFAULT_PUBLIC_BASE_URL
  ) {
    return trimTrailingSlash(configured);
  }

  return trimTrailingSlash(browserOrigin.trim());
};

export const buildOidcProviderCallbackUrl = (
  providerSlug: string,
  baseUrl: string,
): string =>
  `${trimTrailingSlash(baseUrl)}/api/v1/auth/oidc/callback/${encodeURIComponent(providerSlug)}`;

export const createEmptyOidcProviderForm = (): OidcProviderFormState => ({
  name: "",
  slug: "",
  issuer: "",
  clientId: "",
  clientSecret: "",
  clearClientSecret: false,
  scopes: "openid profile email",
  allowedEmailDomains: "",
  isEnabled: false,
  allowAccountCreation: false,
  requireVerifiedEmail: true,
  defaultRole: UserRole.User,
  syncProfile: true,
  syncAvatar: true,
});

export const createOidcProviderFormFromDto = (
  provider: OidcProviderDto,
): OidcProviderFormState => ({
  name: provider.name,
  slug: provider.slug,
  issuer: provider.issuer,
  clientId: provider.clientId,
  clientSecret: "",
  clearClientSecret: false,
  scopes: provider.scopes.join(" "),
  allowedEmailDomains: provider.allowedEmailDomains.join(", "),
  isEnabled: provider.isEnabled,
  allowAccountCreation: provider.allowAccountCreation,
  requireVerifiedEmail: provider.requireVerifiedEmail,
  defaultRole: provider.defaultRole,
  syncProfile: provider.syncProfile,
  syncAvatar: provider.syncAvatar,
});

const splitList = (value: string): string[] =>
  value
    .split(/[\s,]+/u)
    .map((entry) => entry.trim())
    .filter((entry, index, array) =>
      entry.length > 0 && array.indexOf(entry) === index,
    );

export const buildOidcProviderRequest = (
  state: OidcProviderFormState,
): OidcProviderRequestDto => ({
  name: state.name.trim(),
  slug: state.slug.trim().length > 0 ? state.slug.trim() : null,
  issuer: state.issuer.trim(),
  clientId: state.clientId.trim(),
  clientSecret:
    state.clientSecret.trim().length > 0 ? state.clientSecret.trim() : null,
  clearClientSecret: state.clearClientSecret,
  scopes: splitList(state.scopes),
  isEnabled: state.isEnabled,
  allowAccountCreation: state.allowAccountCreation,
  requireVerifiedEmail: state.requireVerifiedEmail,
  defaultRole: state.defaultRole,
  allowedEmailDomains: splitList(state.allowedEmailDomains).map((domain) =>
    domain.toLowerCase(),
  ),
  syncProfile: state.syncProfile,
  syncAvatar: state.syncAvatar,
});

export const isOidcProviderFormValid = (state: OidcProviderFormState): boolean =>
  state.name.trim().length > 0 &&
  state.issuer.trim().length > 0 &&
  state.clientId.trim().length > 0;

export type OidcProviderFormUpdater = <K extends keyof OidcProviderFormState>(
  key: K,
  value: OidcProviderFormState[K],
) => void;
