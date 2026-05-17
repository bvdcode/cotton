import { Button, Stack, Typography } from "@mui/material";
import { toast } from "react-toastify";

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
  if (toast.isActive(TOAST_ID)) {
    return;
  }

  toast.info(
    <Stack direction="row" spacing={1} alignItems="center">
      <Typography variant="body2" sx={{ flex: 1 }}>
        {message}
      </Typography>
      <Button
        size="small"
        color="primary"
        variant="contained"
        onClick={() => {
          toast.dismiss(TOAST_ID);
          onUpdate();
        }}
      >
        {action}
      </Button>
    </Stack>,
    {
      toastId: TOAST_ID,
      autoClose: false,
      closeOnClick: false,
      closeButton: true,
      position: "bottom-right",
      draggable: false,
    },
  );
}
