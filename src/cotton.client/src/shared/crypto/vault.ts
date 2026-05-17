import { create } from "zustand";
import { NoKeyError } from "./errors";
import { deriveMetadataKey } from "./keys";

export interface VaultState {
  masterKey: CryptoKey | null;
  isUnlocked: boolean;
  unlock: (masterKey: CryptoKey) => void;
  lock: () => void;
}

let metadataKeyCache:
  | { masterKey: CryptoKey; metadataKey: Promise<CryptoKey> }
  | null = null;

export const useVault = create<VaultState>((set) => ({
  masterKey: null,
  isUnlocked: false,
  unlock: (masterKey) => {
    metadataKeyCache = null;
    set({ masterKey, isUnlocked: true });
  },
  lock: () => {
    metadataKeyCache = null;
    set({ masterKey: null, isUnlocked: false });
  },
}));

export function requireMasterKey(): CryptoKey {
  const { masterKey } = useVault.getState();

  if (!masterKey) {
    throw new NoKeyError("Vault is locked.");
  }

  return masterKey;
}

export async function requireMetadataKey(): Promise<CryptoKey> {
  const masterKey = requireMasterKey();

  if (metadataKeyCache?.masterKey === masterKey) {
    return await metadataKeyCache.metadataKey;
  }

  const metadataKey = deriveMetadataKey(masterKey);
  metadataKeyCache = { masterKey, metadataKey };
  return await metadataKey;
}
