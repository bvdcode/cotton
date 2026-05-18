import { showActionToast } from "../ui/ActionToast";

interface ServiceWorkerUpdateToastOptions {
  message: string;
  action: string;
  onUpdate: () => void;
}

const TOAST_ID = "cotton-sw-update-available";

export function showServiceWorkerUpdateToast({
  message,
  action,
  onUpdate,
}: ServiceWorkerUpdateToastOptions): void {
  showActionToast({
    toastId: TOAST_ID,
    message,
    action,
    onAction: onUpdate,
  });
}
