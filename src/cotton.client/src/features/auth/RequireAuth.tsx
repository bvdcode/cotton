import { useAuth } from "./useAuth";
import { useEffect, type ReactNode } from "react";
import Loader from "../../shared/ui/Loader";
import { Navigate, useLocation } from "react-router-dom";

type Props = {
  children: ReactNode;
};

export function RequireAuth({ children }: Props) {
  const {
    isAuthenticated,
    isInitializing,
    hydrated,
    hasChecked,
    ensureAuth,
  } = useAuth();
  const location = useLocation();

  useEffect(() => {
    ensureAuth();
  }, [ensureAuth]);

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

  // Wait for the first auth check to finish to avoid transient login flashes.
  if (!isAuthenticated && !hasChecked) {
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
