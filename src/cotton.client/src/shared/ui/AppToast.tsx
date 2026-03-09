import { Snackbar } from "@mui/material";

export interface AppToastState {
  open: boolean;
  message: string;
}

interface AppToastProps {
  toast: AppToastState;
  onClose: () => void;
  autoHideDuration?: number;
}

export const AppToast = ({
  toast,
  onClose,
  autoHideDuration = 2500,
}: AppToastProps) => {
  return (
    <Snackbar
      open={toast.open}
      autoHideDuration={autoHideDuration}
      onClose={onClose}
      message={toast.message}
    />
  );
};
