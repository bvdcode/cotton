export type PublicKeyCredentialCreationOptionsJson = Omit<
  PublicKeyCredentialCreationOptions,
  "challenge" | "excludeCredentials" | "user"
> & {
  challenge: string;
  excludeCredentials?: Array<
    Omit<PublicKeyCredentialDescriptor, "id"> & { id: string }
  >;
  user: Omit<PublicKeyCredentialUserEntity, "id"> & { id: string };
};

export type PublicKeyCredentialRequestOptionsJson = Omit<
  PublicKeyCredentialRequestOptions,
  "allowCredentials" | "challenge"
> & {
  allowCredentials?: Array<
    Omit<PublicKeyCredentialDescriptor, "id"> & { id: string }
  >;
  challenge: string;
};

export interface SerializedAttestationCredential {
  id: string;
  rawId: string;
  type: string;
  transports: string[];
  response: {
    attestationObject: string;
    clientDataJson: string;
  };
}

export interface SerializedAssertionCredential {
  id: string;
  rawId: string;
  type: string;
  response: {
    authenticatorData: string;
    clientDataJson: string;
    signature: string;
    userHandle: string | null;
  };
}

export const isPasskeySupported = (): boolean => {
  return (
    typeof window !== "undefined" &&
    typeof PublicKeyCredential !== "undefined" &&
    typeof navigator.credentials?.create === "function" &&
    typeof navigator.credentials?.get === "function"
  );
};

export const base64UrlToBuffer = (value: string): ArrayBuffer => {
  const normalized = value.replace(/-/g, "+").replace(/_/g, "/");
  const padded = normalized.padEnd(
    normalized.length + ((4 - (normalized.length % 4)) % 4),
    "=",
  );
  const binary = window.atob(padded);
  const bytes = new Uint8Array(binary.length);

  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i);
  }

  return bytes.buffer;
};

export const bufferToBase64Url = (buffer: ArrayBuffer): string => {
  const bytes = new Uint8Array(buffer);
  let binary = "";

  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }

  return window
    .btoa(binary)
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/g, "");
};

export const toCredentialCreationOptions = (
  options: PublicKeyCredentialCreationOptionsJson,
): PublicKeyCredentialCreationOptions => {
  return {
    ...options,
    challenge: base64UrlToBuffer(options.challenge),
    excludeCredentials: options.excludeCredentials?.map((credential) => ({
      ...credential,
      id: base64UrlToBuffer(credential.id),
    })),
    user: {
      ...options.user,
      id: base64UrlToBuffer(options.user.id),
    },
  };
};

export const toCredentialRequestOptions = (
  options: PublicKeyCredentialRequestOptionsJson,
): PublicKeyCredentialRequestOptions => {
  return {
    ...options,
    allowCredentials: options.allowCredentials?.map((credential) => ({
      ...credential,
      id: base64UrlToBuffer(credential.id),
    })),
    challenge: base64UrlToBuffer(options.challenge),
  };
};

export const serializeAttestationCredential = (
  credential: PublicKeyCredential,
): SerializedAttestationCredential => {
  const response = credential.response as AuthenticatorAttestationResponse & {
    getTransports?: () => string[];
  };

  return {
    id: credential.id,
    rawId: bufferToBase64Url(credential.rawId),
    type: credential.type,
    transports: response.getTransports?.() ?? [],
    response: {
      attestationObject: bufferToBase64Url(response.attestationObject),
      clientDataJson: bufferToBase64Url(response.clientDataJSON),
    },
  };
};

export const serializeAssertionCredential = (
  credential: PublicKeyCredential,
): SerializedAssertionCredential => {
  const response = credential.response as AuthenticatorAssertionResponse;

  return {
    id: credential.id,
    rawId: bufferToBase64Url(credential.rawId),
    type: credential.type,
    response: {
      authenticatorData: bufferToBase64Url(response.authenticatorData),
      clientDataJson: bufferToBase64Url(response.clientDataJSON),
      signature: bufferToBase64Url(response.signature),
      userHandle: response.userHandle
        ? bufferToBase64Url(response.userHandle)
        : null,
    },
  };
};
