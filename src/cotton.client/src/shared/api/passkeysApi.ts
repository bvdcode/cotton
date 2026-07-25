import { httpClient, setAccessToken } from "./httpClient";
import type {
  PublicKeyCredentialCreationOptionsJson,
  PublicKeyCredentialRequestOptionsJson,
  SerializedAssertionCredential,
  SerializedAttestationCredential,
} from "../passkeys/webauthn";

export interface PasskeyCredential {
  id: string;
  label: string | null;
  credentialId: string;
  transports: string[];
  aaGuid: string;
  authenticatorName: string | null;
  authenticatorKind: PasskeyAuthenticatorKind;
  isBackupEligible: boolean;
  isBackedUp: boolean;
  createdAt: string;
  lastUsedAt: string | null;
}

export type PasskeyAuthenticatorKind = "Unknown" | "SecurityKey" | "Device";

interface PasskeyRegistrationOptionsResponse {
  requestId: string;
  options: PublicKeyCredentialCreationOptionsJson;
}

interface PasskeyAssertionOptionsResponse {
  requestId: string;
  options: PublicKeyCredentialRequestOptionsJson;
}

interface TokenPairResponse {
  accessToken: string;
}

export const passkeysApi = {
  list: async (): Promise<PasskeyCredential[]> => {
    const response = await httpClient.get<PasskeyCredential[]>("auth/passkeys");
    return response.data;
  },

  beginRegistration: async (
    label?: string | null,
  ): Promise<PasskeyRegistrationOptionsResponse> => {
    const response = await httpClient.post<PasskeyRegistrationOptionsResponse>(
      "auth/passkeys/registration/options",
      { label: label?.trim() || null },
    );
    return response.data;
  },

  finishRegistration: async (
    requestId: string,
    label: string | null,
    credential: SerializedAttestationCredential,
  ): Promise<PasskeyCredential> => {
    const response = await httpClient.post<PasskeyCredential>(
      "auth/passkeys/registration/verify",
      { requestId, label: label?.trim() || null, credential },
    );
    return response.data;
  },

  delete: async (credentialId: string): Promise<void> => {
    await httpClient.delete(
      `auth/passkeys/${encodeURIComponent(credentialId)}`,
    );
  },

  setLabel: async (
    credentialId: string,
    label: string | null,
  ): Promise<PasskeyCredential> => {
    const response = await httpClient.put<PasskeyCredential>(
      `auth/passkeys/${encodeURIComponent(credentialId)}`,
      { label: label?.trim() || null },
    );
    return response.data;
  },

  beginAssertion: async (
    username?: string | null,
  ): Promise<PasskeyAssertionOptionsResponse> => {
    const response = await httpClient.post<PasskeyAssertionOptionsResponse>(
      "auth/passkeys/assertion/options",
      { username: username?.trim() || null },
    );
    return response.data;
  },

  finishAssertion: async (
    requestId: string,
    trustDevice: boolean,
    credential: SerializedAssertionCredential,
  ): Promise<string> => {
    const response = await httpClient.post<TokenPairResponse>(
      "auth/passkeys/assertion/verify",
      { requestId, trustDevice, credential },
    );
    const token = response.data.accessToken;
    setAccessToken(token);
    return token;
  },
};
