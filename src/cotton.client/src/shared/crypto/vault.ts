import { create } from "zustand";
import { CLIENT_ENCRYPTION_SESSION_KEY } from "../config/storageKeys";
import { base64ToBytes, bytesToBase64 } from "./base64";
import { NoKeyError } from "./errors";
import { deriveMetadataKey, exportMasterKey, importMasterKey } from "./keys";

export interface VaultState {
  masterKey: CryptoKey | null;
  isUnlocked: boolean;
  unlock: (
    masterKey: CryptoKey,
    options?: { persistToSession?: boolean },
  ) => void;
  lock: () => void;
}

let metadataKeyCache:
  | { masterKey: CryptoKey; metadataKey: Promise<CryptoKey> }
  | null = null;
let sessionPersistenceVersion = 0;

export const useVault = create<VaultState>((set) => ({
  masterKey: null,
  isUnlocked: false,
  unlock: (masterKey, options) => {
    metadataKeyCache = null;
    set({ masterKey, isUnlocked: true });

    if (options?.persistToSession === false) {
      return;
    }

    void persistVaultSessionKey(masterKey);
  },
  lock: () => {
    metadataKeyCache = null;
    clearVaultSession();
    set({ masterKey: null, isUnlocked: false });
  },
}));

export function clearVaultSession(): void {
  sessionPersistenceVersion += 1;
  try {
    sessionStorage.removeItem(CLIENT_ENCRYPTION_SESSION_KEY);
  } catch {
    // best-effort: storage APIs can be unavailable in hardened environments
  }
}

export async function persistCurrentVaultSession(): Promise<boolean> {
  const masterKey = useVault.getState().masterKey;
  if (!masterKey) {
    clearVaultSession();
    return false;
  }

  await persistVaultSessionKey(masterKey);
  return true;
}

export async function restoreVaultFromSession(): Promise<boolean> {
  let encoded: string | null;
  try {
    encoded = sessionStorage.getItem(CLIENT_ENCRYPTION_SESSION_KEY);
  } catch {
    return false;
  }

  if (!encoded) {
    return false;
  }

  let raw: Uint8Array | null = null;
  try {
    raw = base64ToBytes(encoded);
    const masterKey = await importMasterKey(raw);
    useVault.getState().unlock(masterKey, { persistToSession: false });
    return true;
  } catch {
    clearVaultSession();
    return false;
  } finally {
    raw?.fill(0);
  }
}

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

async function persistVaultSessionKey(masterKey: CryptoKey): Promise<void> {
  const version = sessionPersistenceVersion + 1;
  sessionPersistenceVersion = version;

  const raw = await exportMasterKey(masterKey);
  try {
    const current = useVault.getState();
    if (
      sessionPersistenceVersion !== version ||
      current.masterKey !== masterKey ||
      !current.isUnlocked
    ) {
      return;
    }

    sessionStorage.setItem(CLIENT_ENCRYPTION_SESSION_KEY, bytesToBase64(raw));
  } catch {
    // best-effort: losing the session cache only means the next refresh locks
  } finally {
    raw.fill(0);
  }
}
