import { useLocalPreferencesStore } from "./localPreferencesStore";
import { useMoveClipboardStore } from "./moveClipboardStore";
import { useNodesStore } from "./nodesStore";
import { useAudioPlayerStore } from "./audioPlayerStore";
import { useUserPreferencesStore } from "./userPreferencesStore";
import { useSetupStatusStore } from "./setupStatusStore";
import { LANGUAGE_STORAGE_KEY } from "../config/storageKeys";
import { clearNotificationCaches } from "../api/queries/notifications";
import { clearLayoutsCaches } from "../api/queries/layouts";
import { clearAdminCaches } from "../api/queries/admin";
import { clearAudioCaches } from "../api/queries/audio";
import { clearTrashCaches } from "../api/queries/trash";
import { queryClient } from "../api/queries/queryClient";
import { useVault } from "../crypto";

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
 * Clears all user-scoped client caches (in-memory and persisted) when auth
 * identity changes to prevent cross-user data leak.
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
  const shouldLockVault =
    nextUserId === null || (nodesOwner !== null && nodesOwner !== nextUserId);
  if (shouldLockVault) {
    useVault.getState().lock();
  }

  if (nodesOwner !== nextUserId) {
    safeClearPersisted(useNodesStore.persist.clearStorage);
    useNodesStore.getState().reset(nextUserId);
    useLocalPreferencesStore.getState().setDeveloperSettingsUnlocked(false);
  }

  // Non-persisted but user-scoped state
  clearNotificationCaches(queryClient);
  clearLayoutsCaches(queryClient);
  clearAdminCaches(queryClient);
  clearAudioCaches(queryClient);
  clearTrashCaches(queryClient);
  useUserPreferencesStore.getState().reset();
  useAudioPlayerStore.getState().reset();
  useSetupStatusStore.getState().reset();
  useMoveClipboardStore.getState().clear();
};
