import type { RouteConfig } from "./types";
import { RequireAuth } from "../features/auth";
import { Routes, Route } from "react-router-dom";
import { FilesPage, HomePage, LoginPage, NotFoundPage, OnboardingPage, ProfilePage, TrashPage } from "../pages";
import { AppLayout, PublicLayout } from "./layouts";
import { Folder, Home, Person, Delete } from "@mui/icons-material";
import { SetupWizardPage } from "../pages/setup/SetupWizardPage";
import { SetupGate } from "../features/settings/SetupGate";

const publicRoutes: RouteConfig[] = [
  { path: "/login", displayName: "Login", element: <LoginPage /> },
];

const appRoutes: RouteConfig[] = [
  {
    path: "/",
    icon: <Home />,
    protected: true,
    displayName: "Home",
    element: <HomePage />,
  },
  {
    path: "/files",
    icon: <Folder />,
    protected: true,
    displayName: "Files",
    element: <FilesPage />,
  },
  {
    path: "/trash",
    icon: <Delete />,
    protected: true,
    displayName: "Trash",
    element: <TrashPage />,
  },
  {
    path: "/profile",
    icon: <Person />,
    protected: true,
    displayName: "Profile",
    element: <ProfilePage />,
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
            <SetupGate>
              <AppLayout routes={appRoutes} />
            </SetupGate>
          </RequireAuth>
        }
      >
        {appRoutes.map((route) => (
          <Route key={route.path} path={route.path} element={route.element} />
        ))}

        {/* Deep link into a specific folder by node id */}
        <Route path="/files/:nodeId" element={<FilesPage />} />
        <Route path="/trash/:nodeId" element={<TrashPage />} />
      </Route>

      <Route
        path="/setup"
        element={
          <RequireAuth>
            <SetupGate>
              <SetupWizardPage />
            </SetupGate>
          </RequireAuth>
        }
      />

      <Route
        path="/onboarding"
        element={
          <RequireAuth>
            <OnboardingPage />
          </RequireAuth>
        }
      />

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}
