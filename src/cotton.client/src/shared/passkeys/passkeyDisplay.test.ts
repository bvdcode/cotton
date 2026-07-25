import { describe, expect, it } from "vitest";
import type { PasskeyCredential } from "../api/passkeysApi";
import {
  resolvePasskeyDisplayName,
  type PasskeyDefaultNames,
} from "./passkeyDisplay";

const defaultNames: PasskeyDefaultNames = {
  Unknown: "Passkey",
  SecurityKey: "Security key",
  Device: "Device passkey",
};

const createCredential = (
  values: Partial<PasskeyCredential>,
): PasskeyCredential => ({
  id: "credential-1",
  label: null,
  credentialId: "credential-id",
  transports: [],
  aaGuid: "00000000-0000-0000-0000-000000000000",
  authenticatorName: null,
  authenticatorKind: "Unknown",
  isBackupEligible: false,
  isBackedUp: false,
  createdAt: "2026-07-10T00:00:00Z",
  lastUsedAt: null,
  ...values,
});

describe("resolvePasskeyDisplayName", () => {
  it("prefers the user label over detected identity", () => {
    const credential: PasskeyCredential = createCredential({
      label: "Office key",
      authenticatorName: "YubiKey 5 Series",
      authenticatorKind: "SecurityKey",
    });

    expect(resolvePasskeyDisplayName(credential, defaultNames)).toBe(
      "Office key",
    );
  });

  it("uses detected identity when no user label exists", () => {
    const credential: PasskeyCredential = createCredential({
      authenticatorName: "Google Password Manager",
      authenticatorKind: "Device",
    });

    expect(resolvePasskeyDisplayName(credential, defaultNames)).toBe(
      "Google Password Manager",
    );
  });

  it("uses the localized generic kind when exact detection is unavailable", () => {
    const credential: PasskeyCredential = createCredential({
      authenticatorKind: "SecurityKey",
    });

    expect(resolvePasskeyDisplayName(credential, defaultNames)).toBe(
      "Security key",
    );
  });
});
