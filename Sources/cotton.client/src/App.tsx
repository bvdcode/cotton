import {
  NotFound,
  AppLayout,
  Dashboard,
  ProtectedRoute,
  RealtimeProvider,
} from "./components";
import {
  Route,
  Routes,
  Navigate,
  BrowserRouter as Router,
} from "react-router-dom";
import React from "react";
import { Box } from "@mui/material";
import Login from "./components/Login";
import "react-toastify/dist/ReactToastify.css";
import { ToastContainer } from "react-toastify";
import { ConfirmProvider } from "material-ui-confirm";
import AppThemeProvider from "./providers/ThemeProvider";
import AuthInitializer from "./components/AuthInitializer";
import { useThemeModeContext } from "./providers/ThemeContext";

const ProtectedAppLayout: React.FC = () => (
  <ProtectedRoute>
    <AppLayout />
  </ProtectedRoute>
);

const protectedRoutes: { path: string; element: React.ReactNode }[] = [
  { path: "/", element: <Dashboard /> },
  { path: "/dashboard", element: <Dashboard /> },
];

const InnerApp: React.FC = () => {
  const { resolvedMode } = useThemeModeContext();
  return (
    <>
      <ConfirmProvider>
        <ToastContainer
          theme={(resolvedMode as "light" | "dark" | "colored") ?? "light"}
        />
        <AuthInitializer />
        <RealtimeProvider />
        <Router>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route path="/" element={<Navigate to="/app" replace />} />

            <Route element={<ProtectedAppLayout />}>
              {protectedRoutes.map(({ path, element }) => (
                <Route key={path} path={path} element={element} />
              ))}
            </Route>

            <Route path="*" element={<NotFound />} />
          </Routes>
        </Router>
      </ConfirmProvider>
    </>
  );
};

function App() {
  return (
    <Box sx={{ position: "fixed", inset: 0 }}>
      <AppThemeProvider>
        <InnerApp />
      </AppThemeProvider>
    </Box>
  );
}

export default App;
