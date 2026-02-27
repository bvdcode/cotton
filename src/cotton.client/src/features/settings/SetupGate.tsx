import { useEffect } from "react";
import { Navigate, useLocation } from "react-router-dom";
import Loader from "../../shared/ui/Loader";
import { useServerInfoStore } from "../../shared/store/serverInfoStore";
import { useAuth } from "../auth";

type Props = {
  children: React.ReactNode;
};

/**
 * Guards routes based on server initialization state.
 * - Fetches public server info (no auth required)
 * - Redirects to /setup if server is not initialized
 * - Redirects from /setup to home when server is initialized
 */
export function SetupGate({ children }: Props) {
  const location = useLocation();
  const { isAuthenticated } = useAuth();
  const data = useServerInfoStore((s) => s.data);
  const loaded = useServerInfoStore((s) => s.loaded);
  const loading = useServerInfoStore((s) => s.loading);
  const fetchServerInfo = useServerInfoStore((s) => s.fetchServerInfo);

  useEffect(() => {
    if (!isAuthenticated) return;
    if (loaded || loading) return;
    fetchServerInfo();
  }, [isAuthenticated, loaded, loading, fetchServerInfo]);

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