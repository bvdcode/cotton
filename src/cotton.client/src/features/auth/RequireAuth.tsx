import { useAuth } from "./useAuth";
import { useEffect, type ReactNode } from "react";
import Loader from "../../shared/ui/Loader";
import { Navigate, useLocation } from "react-router-dom";
import { useSettingsStore } from "../../shared/store/settingsStore";

type Props = {
  children: ReactNode;
};

export function RequireAuth({ children }: Props) {
  const {
    isAuthenticated,
    isInitializing,
    hydrated,
    hasChecked,
    refreshEnabled,
    ensureAuth,
  } = useAuth();
  const location = useLocation();
  const settingsLoaded = useSettingsStore((s) => s.loaded);
  const settingsLoading = useSettingsStore((s) => s.loading);
  const fetchSettings = useSettingsStore((s) => s.fetchSettings);

  useEffect(() => {
    ensureAuth();
  }, [ensureAuth]);

  useEffect(() => {
    if (!isAuthenticated) return;
    if (settingsLoaded || settingsLoading) return;
    fetchSettings();
  }, [isAuthenticated, settingsLoaded, settingsLoading, fetchSettings]);

  useEffect(() => {
    if (!isAuthenticated) return;

    const handleFocus = () => {
      fetchSettings({ force: true });
    };

    window.addEventListener("focus", handleFocus);

    return () => {
      window.removeEventListener("focus", handleFocus);
    };
  }, [isAuthenticated, fetchSettings]);

  // Wait for store rehydration before deciding to redirect.
  if (!hydrated) {
    return (
      <Loader
        overlay={true}
        title="Loading..."
        caption="Please, wait"
      />
    );
  }

  if (isInitializing) {
    return (
      <Loader
        overlay={true}
        title="Checking authorization..."
        caption="Please, wait"
      />
    );
  }

  // If refresh is allowed and we haven't checked yet, show loader instead of flashing login.
  if (!isAuthenticated && refreshEnabled && !hasChecked) {
    return (
      <Loader
        overlay={true}
        title="Checking authorization..."
        caption="Please, wait"
      />
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  }

  return <>{children}</>;
}
