import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { useAuth } from "./useAuth";
import { UserRole } from "./types";

type RequireAdminProps = {
  children: ReactNode;
};

export const RequireAdmin = ({ children }: RequireAdminProps) => {
  const { user } = useAuth();

  if (!user) {
    return null;
  }

  if (user.role !== UserRole.Admin) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
};
