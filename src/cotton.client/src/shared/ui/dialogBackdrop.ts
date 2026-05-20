export const blurredDialogBackdropSlotProps = {
  backdrop: {
    sx: {
      backdropFilter: "blur(10px)",
      WebkitBackdropFilter: "blur(10px)",
      backgroundColor: "rgba(8, 12, 18, 0.38)",
    },
  },
} as const;
