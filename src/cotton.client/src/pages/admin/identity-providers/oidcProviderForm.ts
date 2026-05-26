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
