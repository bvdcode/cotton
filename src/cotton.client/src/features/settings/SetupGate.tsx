import { useEffect } from "react";
import { Navigate, useLocation } from "react-router-dom";
import Loader from "../../shared/ui/Loader";
import { useAuth } from "../auth";
import { UserRole } from "../auth/types";
import { useSetupStatusStore } from "../../shared/store/setupStatusStore";

type Props = {
  children: React.ReactNode;
};

/**
 * Guards routes based on server initialization state.
 * - Fetches setup completion status via protected endpoint for admins
 * - Redirects to /setup if server is not initialized
 * - Redirects from /setup to home when server is initialized
 */
export function SetupGate({ children }: Props) {
  const location = useLocation();
  const { isAuthenticated, user } = useAuth();
  const isInitialized = useSetupStatusStore((s) => s.isInitialized);
  const loaded = useSetupStatusStore((s) => s.loaded);
  const loading = useSetupStatusStore((s) => s.loading);
  const fetchSetupStatus = useSetupStatusStore((s) => s.fetchSetupStatus);

  const isAdmin = user?.role === UserRole.Admin;

  useEffect(() => {
    if (!isAuthenticated || !isAdmin) return;
    void fetchSetupStatus({ force: true });
  }, [isAuthenticated, isAdmin, fetchSetupStatus]);

  if (isAdmin && (!loaded || loading)) {
    return (
      <Loader
        overlay={true}
        title="Loading settings..."
        caption="Please, wait"
      />
    );
  }

  const setupCompleted = isAdmin ? (isInitialized ?? true) : true;
  const onSetupPage = location.pathname === "/setup";

  if (!setupCompleted && !onSetupPage) {
    return <Navigate to="/setup" replace />;
  }

  if (setupCompleted && onSetupPage) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}