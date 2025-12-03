import darkTheme from "../themes/darkTheme";
import lightTheme from "../themes/lightTheme";
import { useEffect, useMemo, useState } from "react";

export type ThemeMode = "light" | "dark" | "system";

export function useSystemPrefersDark() {
  const [prefersDark, setPrefersDark] = useState<boolean>(() =>
    typeof window !== "undefined"
      ? window.matchMedia("(prefers-color-scheme: dark)").matches
      : false,
  );
  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }
    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    const listener = (e: MediaQueryListEvent) => setPrefersDark(e.matches);
    mq.addEventListener?.("change", listener);
    return () => mq.removeEventListener?.("change", listener);
  }, []);
  return prefersDark;
}

export const THEME_STORAGE_KEY = "ui-theme-mode";

export function useThemeMode() {
  const prefersDark = useSystemPrefersDark();
  const [mode, setMode] = useState<ThemeMode>(() => {
    try {
      const stored = localStorage.getItem(THEME_STORAGE_KEY);
      if (stored === "light" || stored === "dark" || stored === "system") {
        return stored;
      }
    } catch {
      /* ignore */
    }
    return "system";
  });

  useEffect(() => {
    try {
      localStorage.setItem(THEME_STORAGE_KEY, mode);
    } catch {
      void 0; // ignore write errors
    }
  }, [mode]);

  const resolvedMode: "light" | "dark" = useMemo(() => {
    if (mode === "system") {
      return prefersDark ? "dark" : "light";
    }
    return mode;
  }, [mode, prefersDark]);

  const theme = resolvedMode === "dark" ? darkTheme : lightTheme;

  const toggle = () => setMode((m) => (m === "light" ? "dark" : "light"));

  return { mode, setMode, resolvedMode, theme, toggle };
}
