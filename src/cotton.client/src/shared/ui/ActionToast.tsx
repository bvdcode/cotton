import { Button } from "@mui/material";
import {
  toast,
  type ToastOptions,
  type TypeOptions,
} from "@shared/ui/notifications";
import type { ReactNode } from "react";

interface ShowActionToastOptions {
  toastId: string;
  message: ReactNode;
  action: ReactNode;
  onAction: () => void;
  type?: TypeOptions;
  autoClose?: ToastOptions["autoClose"];
}

export function showActionToast({
  toastId,
  message,
  action,
  onAction,
  type = "info",
  autoClose = false,
}: ShowActionToastOptions): void {
  if (toast.isActive(toastId)) {
    return;
  }

  toast(message, {
    action: (
      <Button
        size="small"
        color={type === "warning" ? "warning" : "primary"}
        variant="contained"
        onClick={() => {
          toast.dismiss(toastId);
          onAction();
        }}
      >
        {action}
      </Button>
    ),
    toastId,
    type,
    autoClose,
    closeOnClick: false,
    closeButton: true,
    position: "bottom-right",
    draggable: false,
  });
}
