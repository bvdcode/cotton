import { useAuth } from "./useAuth";
import { type ReactNode } from "react";
import Loader from "../../shared/ui/Loader";
import { Navigate, useLocation } from "react-router-dom";

type Props = {
  children: ReactNode;
};

export function RequireAuth({ children }: Props) {
  const { isAuthenticated, isInitializing } = useAuth();
  const location = useLocation();

  console.log("[RequireAuth] Checking auth:", { isInitializing, isAuthenticated, path: location.pathname });

  if (isInitializing) {
    console.log("[RequireAuth] Still initializing, showing loader");
    return (
      <Loader
        overlay={true}
        title="Checking authorization..."
        caption="Please, wait"
      />
    );
  }

  if (!isAuthenticated) {
    console.log("[RequireAuth] Not authenticated, redirecting to /login");
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  }

  console.log("[RequireAuth] Authenticated, rendering children");
  return <>{children}</>;
}
