import type { RouteConfig } from "./types";
import { RequireAuth } from "../features/auth";
import { Routes, Route } from "react-router-dom";
import { LoginPage, NotFoundPage } from "../pages";
import { AppLayout, PublicLayout } from "./layouts";
import { Dashboard, Home } from "@mui/icons-material";
import { Box } from "@mui/material";

const publicRoutes: RouteConfig[] = [
  { path: "/login", displayName: "Login", element: <LoginPage /> },
];

const appRoutes: RouteConfig[] = [
  {
    path: "/",
    icon: <Home />,
    protected: true,
    displayName: "Home",
    element: (
      <Box bgcolor="green" width="100%" height="100%">
        Home
      </Box>
    ),
  },
  {
    protected: true,
    path: "/dashboard",
    icon: <Dashboard />,
    displayName: "Dashboard",
    element: (
      <Box width="100%" bgcolor="red" display="flex" flexDirection="column">
        Dashboard
        {/* Generate 100 random rows */}
        <Box>
          {Array.from({ length: 100 }).map((_, index) => (
            <Box key={index} p={1} borderBottom="1px solid #ccc">
              Row {index + 1}
            </Box>
          ))}
        </Box>
      </Box>
    ),
  },
];

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<PublicLayout />}>
        {publicRoutes.map((route) => (
          <Route key={route.path} path={route.path} element={route.element} />
        ))}
      </Route>

      <Route
        element={
          <RequireAuth>
            <AppLayout routes={appRoutes} />
          </RequireAuth>
        }
      >
        {appRoutes.map((route) => (
          <Route key={route.path} path={route.path} element={route.element} />
        ))}
      </Route>

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}
