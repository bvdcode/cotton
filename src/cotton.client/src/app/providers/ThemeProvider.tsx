import { useMemo } from "react";
import type { ReactNode } from "react";
import { ThemeContext } from "./ThemeContext";
import { darkTheme, lightTheme } from "../../shared/theme";
import { usePreferencesStore } from "../../shared/store/preferencesStore";
import { ThemeProvider as MuiThemeProvider, CssBaseline } from "@mui/material";

interface ThemeContextProviderProps {
  children: ReactNode;
}

export const ThemeContextProvider = ({
  children,
}: ThemeContextProviderProps) => {
  const mode = usePreferencesStore((state) => state.theme);
  const setTheme = usePreferencesStore((state) => state.setTheme);
  const resolvedMode = useMemo(() => {
    if (mode === "system") {
      return window.matchMedia("(prefers-color-scheme: dark)").matches
        ? "dark"
        : "light";
    }
    return mode;
  }, [mode]);

  const theme = useMemo(() => {
    return resolvedMode === "light" ? lightTheme : darkTheme;
  }, [resolvedMode]);

  const contextValue = useMemo(
    () => ({ mode, resolvedMode, setTheme }),
    [mode, resolvedMode, setTheme],
  );

  return (
    <ThemeContext.Provider value={contextValue}>
      <MuiThemeProvider theme={theme}>
        <CssBaseline />
        {children}
      </MuiThemeProvider>
    </ThemeContext.Provider>
  );
};
