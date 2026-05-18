import { Button, Stack, Typography } from "@mui/material";
import { toast, type ToastOptions, type TypeOptions } from "react-toastify";
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

  toast(
    <Stack direction="row" spacing={1} alignItems="center">
      <Typography variant="body2" sx={{ flex: 1 }}>
        {message}
      </Typography>
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
    </Stack>,
    {
      toastId,
      type,
      autoClose,
      closeOnClick: false,
      closeButton: true,
      position: "bottom-right",
      draggable: false,
    },
  );
}
