import type { PropsWithChildren } from "react";
import { ToastContainer } from "react-toastify";
import { useThemeMode } from "./useThemeMode.ts";
import { ThemeModeContext } from "./ThemeContext";
import CssBaseline from "@mui/material/CssBaseline";
import { ThemeProvider as MuiThemeProvider } from "@mui/material/styles";

export const AppThemeProvider = ({ children }: PropsWithChildren) => {
  const { theme, toggle, mode, resolvedMode } = useThemeMode();
  return (
    <ThemeModeContext.Provider value={{ toggle, mode, resolvedMode }}>
      <MuiThemeProvider theme={theme}>
        <CssBaseline />
        <ToastContainer theme={mode} />
        {children}
      </MuiThemeProvider>
    </ThemeModeContext.Provider>
  );
};

export default AppThemeProvider;
