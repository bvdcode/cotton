import { useAuth } from "./useAuth";
import { useEffect, type ReactNode } from "react";
import Loader from "../../shared/ui/Loader";
import { Navigate, useLocation } from "react-router-dom";

type Props = {
  children: ReactNode;
};

export function RequireAuth({ children }: Props) {
  const { isAuthenticated, isInitializing, ensureAuth } = useAuth();
  const location = useLocation();

  useEffect(() => {
    ensureAuth();
  }, [ensureAuth]);

  if (isInitializing) {
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
