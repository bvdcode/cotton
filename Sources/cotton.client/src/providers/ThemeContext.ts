import React from "react";

export type ThemeContextValue = {
  toggle: () => void;
  mode: "light" | "dark" | "system";
  resolvedMode: "light" | "dark";
};

export const ThemeModeContext = React.createContext<ThemeContextValue | null>(
  null,
);

export function useThemeModeContext() {
  const ctx = React.useContext(ThemeModeContext);
  if (!ctx) {
    throw new Error("useThemeModeContext must be used within AppThemeProvider");
  }
  return ctx;
}
