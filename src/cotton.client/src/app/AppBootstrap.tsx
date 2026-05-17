import { useEffect } from "react";
import { ToastContainer } from "react-toastify";
import { useEventHub } from "../features/notifications";
import { useTheme } from "./providers";
import i18n from "../i18n";
import {
  selectUiLanguage,
  useUserPreferencesStore,
} from "../shared/store/userPreferencesStore";
import { useUserPreferencesRealtimeEvents } from "../shared/store/useUserPreferencesRealtimeEvents";

export const AppBootstrap = () => {
  useEventHub();
  useUserPreferencesRealtimeEvents();

  const preferredLanguage = useUserPreferencesStore(selectUiLanguage);
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
