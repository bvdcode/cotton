import { create } from "zustand";
import { NoKeyError } from "./errors";

export interface VaultState {
  masterKey: CryptoKey | null;
  isUnlocked: boolean;
  unlock: (masterKey: CryptoKey) => void;
  lock: () => void;
}

export const useVault = create<VaultState>((set) => ({
  masterKey: null,
  isUnlocked: false,
  unlock: (masterKey) => set({ masterKey, isUnlocked: true }),
  lock: () => set({ masterKey: null, isUnlocked: false }),
}));

export function requireMasterKey(): CryptoKey {
  const { masterKey } = useVault.getState();

  if (!masterKey) {
    throw new NoKeyError("Vault is locked.");
  }

  return masterKey;
}
