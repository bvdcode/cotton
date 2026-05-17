import { describe, expect, it } from "vitest";
import { NoKeyError } from "./errors";
import { generateMasterKey } from "./keys";
import { requireMasterKey, requireMetadataKey, useVault } from "./vault";

describe("vault", () => {
  it("keeps the master key in tab memory only", async () => {
    const masterKey = await generateMasterKey();

    useVault.getState().lock();
    expect(() => requireMasterKey()).toThrow(NoKeyError);

    useVault.getState().unlock(masterKey);
    expect(requireMasterKey()).toBe(masterKey);
    expect(useVault.getState().isUnlocked).toBe(true);

    useVault.getState().lock();
    expect(useVault.getState().masterKey).toBeNull();
    expect(useVault.getState().isUnlocked).toBe(false);
  });

  it("derives metadata keys per unlocked master key and clears them on lock", async () => {
    const firstMasterKey = await generateMasterKey();
    const secondMasterKey = await generateMasterKey();

    useVault.getState().lock();
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
