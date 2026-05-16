import type { Theme } from "@mui/material";

export interface ModelPaletteSwatch {
  id: string;
  color: string;
}

export function buildModelPaletteColors(theme: Theme): ModelPaletteSwatch[] {
  return [
    { id: "grey-300", color: theme.palette.grey[300] },
    { id: "grey-500", color: theme.palette.grey[500] },
    { id: "grey-700", color: theme.palette.grey[700] },

    { id: "primary-light", color: theme.palette.primary.light },
    { id: "primary-main", color: theme.palette.primary.main },
    { id: "primary-dark", color: theme.palette.primary.dark },

    { id: "secondary-light", color: theme.palette.secondary.light },
    { id: "secondary-main", color: theme.palette.secondary.main },
    { id: "secondary-dark", color: theme.palette.secondary.dark },

    { id: "info-light", color: theme.palette.info.light },
    { id: "info-main", color: theme.palette.info.main },
    { id: "info-dark", color: theme.palette.info.dark },

    { id: "success-light", color: theme.palette.success.light },
    { id: "success-main", color: theme.palette.success.main },
    { id: "success-dark", color: theme.palette.success.dark },

    { id: "warning-light", color: theme.palette.warning.light },
    { id: "warning-main", color: theme.palette.warning.main },
    { id: "warning-dark", color: theme.palette.warning.dark },

    { id: "error-light", color: theme.palette.error.light },
    { id: "error-main", color: theme.palette.error.main },
    { id: "error-dark", color: theme.palette.error.dark },
  ];
}
