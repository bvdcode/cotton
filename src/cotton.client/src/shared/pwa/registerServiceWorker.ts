import { registerSW } from "virtual:pwa-register";
import i18n from "../../i18n";
import { showServiceWorkerUpdateToast } from "./ServiceWorkerUpdateToast";

export function registerServiceWorker(): void {
  if (import.meta.env.DEV) {
    return;
  }

  const updateSW = registerSW({
    immediate: true,
    onNeedRefresh() {
      showServiceWorkerUpdateToast({
        message: i18n.t("common:pwa.updateAvailable"),
        action: i18n.t("common:pwa.updateAction"),
        onUpdate: () => {
          void updateSW(true);
        },
      });
    },
    onOfflineReady() {
      // The app is already usable offline; surfacing this would just add noise.
    },
  });
}
