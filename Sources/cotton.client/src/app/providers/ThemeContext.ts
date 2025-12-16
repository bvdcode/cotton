import { createContext } from "react";
import type { ThemeMode } from "../../shared/theme";

export interface ThemeContextType {
  mode: ThemeMode;
  resolvedMode: ThemeMode;
  setTheme: (theme: ThemeMode) => void;
}

export const ThemeContext = createContext<ThemeContextType | undefined>(
  undefined,
);
