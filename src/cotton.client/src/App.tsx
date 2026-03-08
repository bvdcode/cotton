import { AppRoutes } from "./app/routes";
import { AuthProvider } from "./features/auth";
import { BrowserRouter } from "react-router-dom";
import { ThemeContextProvider } from "./app/providers";
import { ConfirmProvider } from "material-ui-confirm";
import { useEventHub } from "./features/notifications";
import { AppErrorBoundary } from "./app/components/AppErrorBoundary";
import { useEffect } from "react";
import i18n from "./i18n";
import {
  selectUiLanguage,
  useUserPreferencesStore,
} from "./shared/store/userPreferencesStore";
import { useUserPreferencesRealtimeEvents } from "./shared/store/useUserPreferencesRealtimeEvents";

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

function App() {
  return (
    <AppErrorBoundary>
      <ThemeContextProvider>
        <ConfirmProvider>
          <AuthProvider>
            <EventHubBootstrap />
            <LanguageBootstrap />
            <BrowserRouter>
              <AppRoutes />
            </BrowserRouter>
          </AuthProvider>
        </ConfirmProvider>
      </ThemeContextProvider>
    </AppErrorBoundary>
  );
}

export default App;
