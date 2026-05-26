import type { ConfirmOptions } from "material-ui-confirm";

export const safeConfirmFocusOptions = {
  confirmationButtonProps: { autoFocus: false },
  cancellationButtonProps: { autoFocus: true },
} satisfies Pick<
  ConfirmOptions,
  "confirmationButtonProps" | "cancellationButtonProps"
>;

export const destructiveConfirmOptions = {
  confirmationButtonProps: {
    ...safeConfirmFocusOptions.confirmationButtonProps,
    color: "error",
  },
  cancellationButtonProps: safeConfirmFocusOptions.cancellationButtonProps,
} satisfies Pick<
  ConfirmOptions,
  "confirmationButtonProps" | "cancellationButtonProps"
>;
