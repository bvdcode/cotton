import { useAuth } from "./useAuth";
import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { getSafeAuthReturnPath } from "../../shared/utils/authReturnPath";

type Props = {
  children: ReactNode;
};

export function RequireAuth({ children }: Props) {
  const { phase } = useAuth();
  const location = useLocation();

  if (phase === "booting") {
    return null;
  }

  if (phase === "anonymous") {
    return (
      <Navigate
        to="/login"
        state={{ from: getSafeAuthReturnPath(location.pathname) }}
        replace
      />
    );
  }

  return <>{children}</>;
}
