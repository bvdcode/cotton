import React, { useEffect } from "react";
import { Navigate } from "react-router-dom";
import { useAuth } from "../stores/authStore";
import Box from "@mui/material/Box";
import LinearProgress from "@mui/material/LinearProgress";

interface ProtectedRouteProps {
  children: React.ReactNode;
  requiredRole?: string;
}

const ProtectedRoute: React.FC<ProtectedRouteProps> = ({
  children,
  requiredRole,
}) => {
  const { user, token, ensureLogin } = useAuth();

  useEffect(() => {
    if (!token) {
      // Trigger fake login to obtain token; do not redirect to /login
      ensureLogin().catch(() => {
        /* swallow error, fallback will navigate below */
      });
    }
  }, [token, ensureLogin]);

  // While obtaining token, show a lightweight loader to prevent flicker
  if (!token) {
    return (
      <Box sx={{ p: 2 }}>
        <LinearProgress />
      </Box>
    );
  }

  if (requiredRole && (!user?.role || user.role !== requiredRole)) {
    return <Navigate to="/app" replace />;
  }

  return <>{children}</>;
};

export default ProtectedRoute;
