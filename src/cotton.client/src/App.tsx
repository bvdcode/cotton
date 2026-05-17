import { AppRoutes } from "./app/routes";
import { AuthProvider } from "./features/auth";
import { BrowserRouter } from "react-router-dom";
import { ThemeContextProvider } from "./app/providers";
import { ConfirmProvider } from "material-ui-confirm";
import { useEventHub } from "./features/notifications";
import { useEffect } from "react";
import i18n from "./i18n";
import { useTheme } from "./app/providers";
import {
  selectUiLanguage,
  useUserPreferencesStore,
} from "./shared/store/userPreferencesStore";
import { useUserPreferencesRealtimeEvents } from "./shared/store/useUserPreferencesRealtimeEvents";
import { ToastContainer } from "react-toastify";
import { QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { queryClient } from "./shared/api/queries/queryClient";

const EventHubBootstrap = () => {
  useEventHub();
  useUserPreferencesRealtimeEvents();
  return null;
};

const LanguageBootstrap = () => {
  const preferred = useUserPreferencesStore(selectUiLanguage);

  useEffect(() => {
    if (!preferred) return;
    if (i18n.language === preferred) return;
    i18n.changeLanguage(preferred).catch(() => {
      // ignore
    });
  }, [preferred]);

  return null;
};

const ToastBootstrap = () => {
  const { resolvedMode } = useTheme();

  return (
    <ToastContainer
      theme={(resolvedMode as "light" | "dark" | "colored") ?? "colored"}
      autoClose={4500}
      newestOnTop
      closeOnClick
      pauseOnHover
    />
  );
};

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeContextProvider>
        <ConfirmProvider>
          <AuthProvider>
            <EventHubBootstrap />
            <LanguageBootstrap />
            <ToastBootstrap />
            <BrowserRouter>
              <AppRoutes />
            </BrowserRouter>
          </AuthProvider>
        </ConfirmProvider>
      </ThemeContextProvider>
      {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
    </QueryClientProvider>
  );
}

export default App;
