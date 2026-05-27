import { UserRole } from "@features/auth/types";
import { httpClient } from "./httpClient";
import type { BaseDto } from "./types";

export interface PublicOidcProviderDto {
  name: string;
  slug: string;
}

export interface OidcProviderDto extends BaseDto<string> {
  name: string;
  slug: string;
  issuer: string;
  clientId: string;
  hasClientSecret: boolean;
  scopes: string[];
  isEnabled: boolean;
  allowAccountCreation: boolean;
  requireVerifiedEmail: boolean;
  defaultRole: UserRole;
  allowedEmailDomains: string[];
  syncProfile: boolean;
  syncAvatar: boolean;
}

export interface OidcAuthorizationUrlDto {
  authorizationUrl: string;
}

export interface OidcAuthorizationRequestDto {
  returnUrl: string | null;
  trustDevice: boolean;
}

export interface UserExternalIdentityDto extends BaseDto<string> {
  providerId: string;
  providerName: string;
  providerSlug: string;
  email: string | null;
  emailVerified: boolean;
  displayName: string | null;
  pictureUrl: string | null;
  lastUsedAt: string | null;
}

export interface OidcProviderRequestDto {
  name: string;
  slug: string | null;
  issuer: string;
  clientId: string;
  clientSecret: string | null;
  clearClientSecret: boolean;
  scopes: string[];
  isEnabled: boolean;
  allowAccountCreation: boolean;
  requireVerifiedEmail: boolean;
  defaultRole: UserRole;
  allowedEmailDomains: string[];
  syncProfile: boolean;
  syncAvatar: boolean;
}

const normalizeStringArray = (values: readonly string[]): string[] =>
  values
    .map((value) => value.trim())
    .filter((value, index, array) => value.length > 0 && array.indexOf(value) === index);

const normalizeProviderRequest = (
  request: OidcProviderRequestDto,
): OidcProviderRequestDto => ({
  ...request,
  name: request.name.trim(),
  slug: request.slug?.trim() || null,
  issuer: request.issuer.trim(),
  clientId: request.clientId.trim(),
  clientSecret: request.clientSecret?.trim() || null,
  scopes: normalizeStringArray(request.scopes),
  allowedEmailDomains: normalizeStringArray(
    request.allowedEmailDomains.map((domain) => domain.toLowerCase()),
  ),
});

const normalizeReturnUrl = (returnUrl: string): string => returnUrl.trim();

export const oidcApi = {
  listPublicProviders: async (): Promise<PublicOidcProviderDto[]> => {
    const response = await httpClient.get<PublicOidcProviderDto[]>(
      "auth/oidc/providers",
    );
    return response.data;
  },

  listAdminProviders: async (): Promise<OidcProviderDto[]> => {
    const response = await httpClient.get<OidcProviderDto[]>(
      "auth/oidc/providers/admin",
    );
    return response.data;
  },

  createProvider: async (
    request: OidcProviderRequestDto,
  ): Promise<OidcProviderDto> => {
    const response = await httpClient.post<OidcProviderDto>(
      "auth/oidc/providers",
      normalizeProviderRequest(request),
    );
    return response.data;
  },

  updateProvider: async (
    providerId: string,
    request: OidcProviderRequestDto,
  ): Promise<OidcProviderDto> => {
    const response = await httpClient.put<OidcProviderDto>(
      `auth/oidc/providers/${encodeURIComponent(providerId)}`,
      normalizeProviderRequest(request),
    );
    return response.data;
  },

  deleteProvider: async (providerId: string): Promise<void> => {
    await httpClient.delete(`auth/oidc/providers/${encodeURIComponent(providerId)}`);
  },

  listLinks: async (): Promise<UserExternalIdentityDto[]> => {
    const response = await httpClient.get<UserExternalIdentityDto[]>(
      "auth/oidc/links",
    );
    return response.data;
  },

  unlink: async (identityId: string): Promise<void> => {
    await httpClient.delete(`auth/oidc/links/${encodeURIComponent(identityId)}`);
  },

  createSignInAuthorizationUrl: async (
    providerSlug: string,
    request: OidcAuthorizationRequestDto,
  ): Promise<string> => {
    const response = await httpClient.post<OidcAuthorizationUrlDto>(
      `auth/oidc/start/${encodeURIComponent(providerSlug)}/authorization-url`,
      {
        returnUrl: request.returnUrl ? normalizeReturnUrl(request.returnUrl) : null,
        trustDevice: request.trustDevice,
      },
    );
    return response.data.authorizationUrl;
  },

  createLinkAuthorizationUrl: async (
    providerSlug: string,
    returnUrl: string,
  ): Promise<string> => {
    const response = await httpClient.post<OidcAuthorizationUrlDto>(
      `auth/oidc/link/${encodeURIComponent(providerSlug)}/authorization-url`,
      {
        returnUrl: normalizeReturnUrl(returnUrl),
        trustDevice: false,
      },
    );
    return response.data.authorizationUrl;
  },
};
