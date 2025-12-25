import { useEffect } from "react";
import { Navigate, useLocation } from "react-router-dom";
import Loader from "../../shared/ui/Loader";
import { useSettingsStore } from "../../shared/store/settingsStore";
import { useAuth } from "../auth";

type Props = {
  children: React.ReactNode;
};

/**
 * Guards routes based on server initialization state.
 * - Fetches settings if not loaded yet
 * - Redirects to /setup if server is not initialized
 * - Redirects from /setup to home when server is initialized
 */
export function SetupGate({ children }: Props) {
  const location = useLocation();
  const { isAuthenticated } = useAuth();
  const data = useSettingsStore((s) => s.data);
  const loaded = useSettingsStore((s) => s.loaded);
  const loading = useSettingsStore((s) => s.loading);
  const fetchSettings = useSettingsStore((s) => s.fetchSettings);

  useEffect(() => {
    if (!isAuthenticated) return;
    if (loaded || loading) return;
    fetchSettings();
  }, [isAuthenticated, loaded, loading, fetchSettings]);

  if (!loaded || loading) {
    return (
      <Loader
        overlay={true}
        title="Loading settings..."
        caption="Please, wait"
      />
    );
  }

  const isInitialized = data?.isServerInitialized ?? false;
  const onSetupPage = location.pathname === "/setup";

  if (!isInitialized && !onSetupPage) {
    return <Navigate to="/setup" replace />;
  }

  if (isInitialized && onSetupPage) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}