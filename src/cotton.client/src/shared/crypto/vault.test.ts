import { beforeEach, describe, expect, it } from "vitest";
import { CLIENT_ENCRYPTION_SESSION_KEY } from "../config/storageKeys";
import { NoKeyError } from "./errors";
import { generateMasterKey } from "./keys";
import {
  requireMasterKey,
  requireMetadataKey,
  restoreVaultFromSession,
  useVault,
} from "./vault";

describe("vault", () => {
  beforeEach(() => {
    sessionStorage.removeItem(CLIENT_ENCRYPTION_SESSION_KEY);
    useVault.getState().lock();
  });

  it("keeps the master key in tab memory and clears the session copy on lock", async () => {
    const masterKey = await generateMasterKey();

    expect(() => requireMasterKey()).toThrow(NoKeyError);

    useVault.getState().unlock(masterKey);
    expect(requireMasterKey()).toBe(masterKey);
    expect(useVault.getState().isUnlocked).toBe(true);
    await expectSessionKey();

    useVault.getState().lock();
    expect(useVault.getState().masterKey).toBeNull();
    expect(useVault.getState().isUnlocked).toBe(false);
    expect(sessionStorage.getItem(CLIENT_ENCRYPTION_SESSION_KEY)).toBeNull();
  });

  it("restores the master key from session storage", async () => {
    const masterKey = await generateMasterKey();

    useVault.getState().unlock(masterKey);
    await expectSessionKey();
    useVault.setState({ masterKey: null, isUnlocked: false });

    await expect(restoreVaultFromSession()).resolves.toBe(true);

    expect(useVault.getState().isUnlocked).toBe(true);
    expect(() => requireMasterKey()).not.toThrow();
  });

  it("can unlock without writing a session copy", async () => {
    const masterKey = await generateMasterKey();

    useVault.getState().lock();
    useVault.getState().unlock(masterKey, { persistToSession: false });
    await new Promise((resolve) => window.setTimeout(resolve, 0));

    expect(useVault.getState().isUnlocked).toBe(true);
    expect(sessionStorage.getItem(CLIENT_ENCRYPTION_SESSION_KEY)).toBeNull();
  });

  it("derives metadata keys per unlocked master key and clears them on lock", async () => {
    const firstMasterKey = await generateMasterKey();
    const secondMasterKey = await generateMasterKey();

    await expect(requireMetadataKey()).rejects.toBeInstanceOf(NoKeyError);

    useVault.getState().unlock(firstMasterKey);
    const firstMetadataKey = await requireMetadataKey();
    await expect(requireMetadataKey()).resolves.toBe(firstMetadataKey);

    useVault.getState().lock();
    await expect(requireMetadataKey()).rejects.toBeInstanceOf(NoKeyError);

    useVault.getState().unlock(secondMasterKey);
    await expect(requireMetadataKey()).resolves.not.toBe(firstMetadataKey);
  });
});

async function expectSessionKey(): Promise<void> {
  for (let attempt = 0; attempt < 20; attempt += 1) {
    if (sessionStorage.getItem(CLIENT_ENCRYPTION_SESSION_KEY)) {
      return;
    }
    await new Promise((resolve) => window.setTimeout(resolve, 0));
  }

  expect(sessionStorage.getItem(CLIENT_ENCRYPTION_SESSION_KEY)).toBeTruthy();
}
