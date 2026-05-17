import { describe, expect, it } from "vitest";
import { NoKeyError } from "./errors";
import { generateMasterKey } from "./keys";
import { requireMasterKey, useVault } from "./vault";

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
});
