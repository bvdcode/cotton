import {
  closeSnackbar,
  enqueueSnackbar,
  type SnackbarAction,
  type SnackbarKey,
  type SnackbarMessage,
  type SnackbarOrigin,
  type VariantType,
} from "notistack";

export type TypeOptions = VariantType;

export interface ToastOptions {
  toastId?: SnackbarKey;
  type?: TypeOptions;
  autoClose?: number | false | null;
  action?: SnackbarAction;
  closeOnClick?: boolean;
  closeButton?: boolean;
  draggable?: boolean;
  position?:
    | "top-left"
    | "top-center"
    | "top-right"
    | "bottom-left"
    | "bottom-center"
    | "bottom-right";
}

type ToastFn = {
  (message: SnackbarMessage, options?: ToastOptions): SnackbarKey;
  success: (message: SnackbarMessage, options?: ToastOptions) => SnackbarKey;
  error: (message: SnackbarMessage, options?: ToastOptions) => SnackbarKey;
  info: (message: SnackbarMessage, options?: ToastOptions) => SnackbarKey;
  warning: (message: SnackbarMessage, options?: ToastOptions) => SnackbarKey;
  dismiss: (key?: SnackbarKey) => void;
  isActive: (key: SnackbarKey) => boolean;
};

const activeToastIds = new Set<SnackbarKey>();

const showToast = (
  message: SnackbarMessage,
  options: ToastOptions = {},
): SnackbarKey => {
  if (options.toastId !== undefined && activeToastIds.has(options.toastId)) {
    return options.toastId;
  }

  let snackbarKey: SnackbarKey = options.toastId ?? "";
  snackbarKey = enqueueSnackbar(message, {
    key: options.toastId,
    variant: options.type ?? "default",
    action: options.action,
    anchorOrigin: resolveAnchorOrigin(options.position),
    autoHideDuration: resolveAutoHideDuration(options.autoClose),
    persist: options.autoClose === false,
    onClose: (_event, _reason, key) => {
      activeToastIds.delete(key ?? snackbarKey);
    },
  });
  activeToastIds.add(snackbarKey);
  return snackbarKey;
};

const showVariant = (variant: TypeOptions) => {
  return (message: SnackbarMessage, options: ToastOptions = {}) =>
    showToast(message, { ...options, type: variant });
};

export const toast: ToastFn = Object.assign(showToast, {
  success: showVariant("success"),
  error: showVariant("error"),
  info: showVariant("info"),
  warning: showVariant("warning"),
  dismiss: (key?: SnackbarKey) => {
    if (key === undefined) {
      activeToastIds.clear();
    } else {
      activeToastIds.delete(key);
    }
    closeSnackbar(key);
  },
  isActive: (key: SnackbarKey) => activeToastIds.has(key),
});

function resolveAutoHideDuration(autoClose: ToastOptions["autoClose"]) {
  if (autoClose === false) return null;
  if (autoClose === null) return null;
  return autoClose;
}

function resolveAnchorOrigin(
  position: ToastOptions["position"],
): SnackbarOrigin | undefined {
  switch (position) {
    case "top-left":
      return { vertical: "top", horizontal: "left" };
    case "top-center":
      return { vertical: "top", horizontal: "center" };
    case "top-right":
      return { vertical: "top", horizontal: "right" };
    case "bottom-left":
      return { vertical: "bottom", horizontal: "left" };
    case "bottom-center":
      return { vertical: "bottom", horizontal: "center" };
    case "bottom-right":
      return { vertical: "bottom", horizontal: "right" };
    default:
      return undefined;
  }
}
