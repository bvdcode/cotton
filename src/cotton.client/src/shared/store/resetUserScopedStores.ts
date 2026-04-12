import { useLayoutsStore } from "./layoutsStore";
import { useNodesStore } from "./nodesStore";
import { useNotificationsStore } from "./notificationsStore";
import { useSettingsStore } from "./settingsStore";
import { useTrashStore } from "./trashStore";
import { useAudioPlayerStore } from "./audioPlayerStore";
import { useUserPreferencesStore } from "./userPreferencesStore";
import { LANGUAGE_STORAGE_KEY } from "../config/storageKeys";

const safeClearPersisted = (clearStorage: () => void | Promise<void>): void => {
  try {
    const result = clearStorage();
    if (result instanceof Promise) {
      void result;
    }
  } catch {
    // best-effort: clearing persisted storage should never block auth flows
  }
};

/**
 * Clears all user-scoped client caches when auth identity changes.
 *
 * IMPORTANT:
 * - Clears in-memory caches (Zustand stores)
 * - Clears persisted caches (zustand persist) to prevent cross-user data leak
 */
export const resetUserScopedStores = (nextUserId: string | null): void => {
  if (nextUserId === null) {
    try {
      sessionStorage.removeItem(LANGUAGE_STORAGE_KEY);
    } catch {
      // best-effort: storage APIs can be unavailable in hardened environments
    }
  }

  const nodesOwner = useNodesStore.getState().cacheOwnerUserId;
  if (nodesOwner !== nextUserId) {
    safeClearPersisted(useNodesStore.persist.clearStorage);
    useNodesStore.getState().reset(nextUserId);
  }

  const layoutsOwner = useLayoutsStore.getState().cacheOwnerUserId;
  if (layoutsOwner !== nextUserId) {
    safeClearPersisted(useLayoutsStore.persist.clearStorage);
    useLayoutsStore.getState().reset(nextUserId);
  }

  // Non-persisted but user-scoped stores
  useTrashStore.getState().reset();
  useNotificationsStore.getState().reset();
  useSettingsStore.getState().reset();
  useUserPreferencesStore.getState().reset();
  useAudioPlayerStore.getState().reset();
};
