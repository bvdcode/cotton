import { useEffect } from "react";
import { ToastContainer } from "react-toastify";
import { useEventHub } from "../features/notifications";
import { useTheme } from "./providers";
import i18n from "../i18n";
import {
  clearVaultSession,
  persistCurrentVaultSession,
  restoreVaultFromSession,
  useVault,
} from "../shared/crypto";
import { useAuth } from "../features/auth";
import { useNodesStore } from "../shared/store/nodesStore";
import {
  selectClientEncryptionLockOnRefresh,
  selectUiLanguage,
  useUserPreferencesStore,
} from "../shared/store/userPreferencesStore";
import { useUserPreferencesRealtimeEvents } from "../shared/store/useUserPreferencesRealtimeEvents";

export const AppBootstrap = () => {
  useEventHub();
  useUserPreferencesRealtimeEvents();

  const { isAuthenticated } = useAuth();
  const preferredLanguage = useUserPreferencesStore(selectUiLanguage);
  const preferencesLoaded = useUserPreferencesStore((state) => state.loaded);
  const lockClientEncryptionOnRefresh = useUserPreferencesStore(
    selectClientEncryptionLockOnRefresh,
  );
  const isVaultUnlocked = useVault((state) => state.isUnlocked);

  useEffect(() => {
    let active = true;

    if (
      !isAuthenticated ||
      !preferencesLoaded ||
      lockClientEncryptionOnRefresh ||
      isVaultUnlocked
    ) {
      return () => {
        active = false;
      };
    }

    void restoreVaultFromSession().then((restored) => {
      if (!active || !restored) return;
      void useNodesStore.getState().refreshCachedFileDisplayMetadata();
    });

    return () => {
      active = false;
    };
  }, [
    isAuthenticated,
    isVaultUnlocked,
    lockClientEncryptionOnRefresh,
    preferencesLoaded,
  ]);

  useEffect(() => {
    if (!preferencesLoaded) return;

    if (lockClientEncryptionOnRefresh) {
      clearVaultSession();
      return;
    }

    if (isVaultUnlocked) {
      void persistCurrentVaultSession();
    }
  }, [isVaultUnlocked, lockClientEncryptionOnRefresh, preferencesLoaded]);

  useEffect(() => {
    if (!preferredLanguage) return;
    if (i18n.language === preferredLanguage) return;

    i18n.changeLanguage(preferredLanguage).catch(() => {
      // best-effort: keep the currently active language
    });
  }, [preferredLanguage]);

  const { resolvedMode } = useTheme();

  return (
    <ToastContainer
      theme={(resolvedMode as "light" | "dark" | "colored") ?? "colored"}
      autoClose={4500}
      newestOnTop
      closeOnClick
      pauseOnHover
    />
  );
};
