import type { RouteConfig } from "./types";
import { RequireAuth } from "../features/auth";
import { Routes, Route } from "react-router-dom";
import {
  FilesPage,
  HomePage,
  LoginPage,
  NotFoundPage,
  OnboardingPage,
  ProfilePage,
  TrashPage,
  SearchPage,
  SharePage,
} from "../pages";
import { AppLayout, PublicLayout } from "./layouts";
import { Folder, Home, Person, Delete, Search } from "@mui/icons-material";
import { SetupWizardPage } from "../pages/setup/SetupWizardPage";
import { SetupGate } from "../features/settings/SetupGate";
import i18n from "../i18n";

const publicRoutes: RouteConfig[] = [
  {
    path: "/login",
    displayName: i18n.t("login", { ns: "routes" }),
    element: <LoginPage />,
  },
  {
    path: "/s/:token",
    displayName: "",
    element: <SharePage />,
  },
  {
    path: "/share/:token",
    displayName: "",
    element: <SharePage />,
  },
];

const appRoutes: RouteConfig[] = [
  {
    path: "/",
    icon: <Home />,
    protected: true,
    displayName: i18n.t("home", { ns: "routes" }),
    element: <HomePage />,
  },
  {
    path: "/files",
    icon: <Folder />,
    protected: true,
    displayName: i18n.t("files", { ns: "routes" }),
    element: <FilesPage />,
  },
  {
    path: "/trash",
    icon: <Delete />,
    protected: true,
    displayName: i18n.t("trash", { ns: "routes" }),
    element: <TrashPage />,
  },
  {
    path: "/search",
    icon: <Search />,
    protected: true,
    displayName: i18n.t("search", { ns: "routes" }),
    element: <SearchPage />,
  },
  {
    path: "/profile",
    icon: <Person />,
    protected: true,
    displayName: i18n.t("profile", { ns: "routes" }),
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
