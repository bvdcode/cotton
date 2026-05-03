import type { RouteConfig } from "./types";
import { RequireAdmin, RequireAuth, useAuth } from "../features/auth";
import { useEffect } from "react";
import { Routes, Route, Navigate, useParams, useLocation, matchPath } from "react-router-dom";
import Loader from "../shared/ui/Loader";
import { useTranslation } from "react-i18next";

const RedirectSToShare = () => {
  const { token } = useParams<{ token: string }>();
  return <Navigate to={`/share/${token ?? ""}`} replace />;
};
import {
  FilesPage,
  HomePage,
  LoginPage,
  NotFoundPage,
  OnboardingPage,
  SettingsPage,
  TrashPage,
  SearchPage,
  SharePage,
  AdminLayoutPage,
  AdminUsersPage,
  AdminDatabaseBackupPage,
  AdminStorageStatisticsPage,
  AdminGeneralSettingsPage,
  AdminEmailSettingsPage,
  ResetPasswordPage,
  VerifyEmailPage,
} from "../pages";
import { AppLayout, PublicLayout } from "./layouts";
import { Folder, Home, Delete, Search } from "@mui/icons-material";
import { SetupWizardPage } from "../pages/setup/SetupWizardPage";
import { SetupGate } from "../features/settings/SetupGate";

const publicRoutes: RouteConfig[] = [
  {
    path: "/login",
    element: <LoginPage />,
  },
  {
    path: "/s/:token",
    element: <RedirectSToShare />,
  },
  {
    path: "/share/:token",
    element: <SharePage />,
  },
  {
    path: "/reset-password",
    element: <ResetPasswordPage />,
  },
  {
    path: "/verify-email",
    element: <VerifyEmailPage />,
  },
];

export function AppRoutes() {
  const { t } = useTranslation(["login"]);
  const location = useLocation();
  const {
    hydrated,
    isInitializing,
    isAuthenticated,
    refreshEnabled,
    hasChecked,
    ensureAuth,
  } = useAuth();

  const isPublicRoute = publicRoutes.some((route) =>
    Boolean(
      matchPath(
        { path: route.path, end: true },
        location.pathname,
      ),
    ),
  );

  useEffect(() => {
    if (isPublicRoute) return;
    ensureAuth();
  }, [ensureAuth, isPublicRoute]);

  const isAuthBootstrapPending =
    !isPublicRoute && (
      !hydrated ||
      isInitializing ||
      (!isAuthenticated && refreshEnabled && !hasChecked)
    );

  if (isAuthBootstrapPending) {
    return (
      <Loader
        overlay={true}
        title={t("restoring.title", { ns: "login" })}
        caption={t("restoring.caption", { ns: "login" })}
      />
    );
  }

  const appRoutes: RouteConfig[] = [
    {
      path: "/",
      icon: <Home />,
      protected: true,
      translationKey: "home",
      element: <HomePage />,
    },
    {
      path: "/files",
      icon: <Folder />,
      protected: true,
      translationKey: "files",
      element: <FilesPage />,
    },
    {
      path: "/trash",
      icon: <Delete />,
      protected: true,
      translationKey: "trash",
      element: <TrashPage />,
    },
    {
      path: "/search",
      icon: <Search />,
      protected: true,
      translationKey: "search",
      element: <SearchPage />,
    },
  ];

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
          <Route
            key={route.path}
            path={route.path === "/admin" ? "/admin/*" : route.path}
            element={route.element}
          />
        ))}

        <Route
          path="/admin"
          element={
            <RequireAdmin>
              <AdminLayoutPage />
            </RequireAdmin>
          }
        >
          <Route index element={<Navigate to="users" replace />} />
          <Route path="users" element={<AdminUsersPage />} />
          <Route path="database-backup" element={<AdminDatabaseBackupPage />} />
          <Route
            path="storage-statistics"
            element={<AdminStorageStatisticsPage />}
          />
          <Route
            path="general-settings"
            element={<AdminGeneralSettingsPage />}
          />
          <Route
            path="storage-settings"
            element={<Navigate to="/admin/storage-statistics" replace />}
          />
          <Route path="email-settings" element={<AdminEmailSettingsPage />} />
        </Route>

        {/* Settings page (accessible from avatar menu) */}
        <Route path="/settings" element={<SettingsPage />} />
        <Route path="/profile" element={<Navigate to="/settings" replace />} />

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
