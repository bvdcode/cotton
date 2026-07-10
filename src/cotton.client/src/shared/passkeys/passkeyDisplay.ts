import type {
  PasskeyAuthenticatorKind,
  PasskeyCredential,
} from "../api/passkeysApi";

export type PasskeyDefaultNames = Record<PasskeyAuthenticatorKind, string>;

export const resolvePasskeyDisplayName = (
  credential: PasskeyCredential,
  defaultNames: PasskeyDefaultNames,
): string => {
  return (
    credential.label?.trim() ||
    credential.authenticatorName?.trim() ||
    defaultNames[credential.authenticatorKind]
  );
};
