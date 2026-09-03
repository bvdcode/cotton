import type { RouteConfig } from "./types";
import { RequireAdmin, RequireAuth, useAuth } from "../features/auth";
import { useEffect, useState } from "react";
import {
  Routes,
  Route,
  Navigate,
  useParams,
  useLocation,
  matchPath,
} from "react-router-dom";
import Loader from "../shared/ui/Loader";
import { AppLayout, PublicLayout } from "./layouts";
import { Folder, Home, Delete } from "@mui/icons-material";
import { SetupGate } from "../features/settings/SetupGate";
import {
  startupApi,
  type StartupStatusResponse,
} from "../shared/api/startupApi";
import { HomePage } from "../pages/home";
import { FilesPage } from "../pages/files";
import { LoginPage } from "../pages/login/LoginPage";
import { NotFoundPage } from "../pages/not-found/NotFoundPage";
import { OnboardingPage } from "../pages/onboarding/OnboardingPage";
import { SettingsPage } from "../pages/profile";
import { TrashPage } from "../pages/trash";
import { SearchPage } from "../pages/search/SearchPage";
import { SharePage } from "../pages/share/SharePage";
import { AdminLayoutPage } from "../pages/admin/AdminLayoutPage";
import { AdminUsersPage } from "../pages/admin/users/AdminUsersPage";
import { AdminGroupsPage } from "../pages/admin/groups/AdminGroupsPage";
import { AdminDatabaseBackupPage } from "../pages/admin/database-backup/AdminDatabaseBackupPage";
import { AdminStorageStatisticsPage } from "../pages/admin/storage-statistics/AdminStorageStatisticsPage";
import { AdminStorageSettingsPage } from "../pages/admin/settings/AdminStorageSettingsPage";
import { AdminGeneralSettingsPage } from "../pages/admin/settings/AdminGeneralSettingsPage";
import { AdminPrivacySettingsPage } from "../pages/admin/settings/AdminPrivacySettingsPage";
import { AdminSecurityDiagnosticsPage } from "../pages/admin/security/AdminSecurityDiagnosticsPage";
import { AdminIdentityProvidersPage } from "../pages/admin/identity-providers/AdminIdentityProvidersPage";
import { AdminNotificationsSettingsPage } from "../pages/admin/settings/AdminNotificationsSettingsPage";
import { ResetPasswordPage } from "../pages/reset-password/ResetPasswordPage";
import { VerifyEmailPage } from "../pages/verify-email/VerifyEmailPage";
import { AppCodeApprovalPage } from "../pages/oauth/AppCodeApprovalPage";
import { UnlockPage } from "../pages/unlock/UnlockPage";
import { SetupWizardPage } from "../pages/setup/SetupWizardPage";
import { StartupBlockedPage } from "../pages/startup/StartupBlockedPage";

const RedirectSToShare = () => {
  const { token } = useParams<{ token: string }>();
  return <Navigate to={`/share/${token ?? ""}`} replace />;
};

const publicRoutes: RouteConfig[] = [
  {
    path: "/unlock",
    element: <UnlockPage />,
  },
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
  const location = useLocation();
  const [startupStatus, setStartupStatus] =
    useState<StartupStatusResponse | null>(null);
  const [startupCheckState, setStartupCheckState] = useState<
    "checking" | "ready" | "blocked"
  >("checking");
  const { phase, restoreSession } = useAuth();

  useEffect(() => {
    let cancelled = false;

    startupApi
      .getStatus()
      .then((status) => {
        if (cancelled) return;
        setStartupStatus(status);
        setStartupCheckState(status.blocked ? "blocked" : "ready");
      })
      .catch(() => {
        if (cancelled) return;
        setStartupStatus(null);
        setStartupCheckState("ready");
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const isPublicRoute = publicRoutes.some((route) =>
    Boolean(matchPath({ path: route.path, end: true }, location.pathname)),
  );
  const shouldRestoreSession = !isPublicRoute || location.pathname === "/login";

  useEffect(() => {
    if (!shouldRestoreSession || phase !== "booting") return;
    void restoreSession();
  }, [phase, restoreSession, shouldRestoreSession]);

  const isAuthBootstrapPending = !isPublicRoute && phase === "booting";
  if (
    startupCheckState === "checking" ||
    (startupCheckState === "ready" && isAuthBootstrapPending)
  ) {
    return <Loader overlay={true} />;
  }

  if (startupCheckState === "blocked") {
    return (
      <Routes>
        <Route element={<PublicLayout />}>
          <Route
            path="*"
            element={
              <StartupBlockedPage blocker={startupStatus?.blocker ?? null} />
            }
          />
        </Route>
      </Routes>
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

        <Route path="/search" element={<SearchPage />} />

        <Route
          path="/admin"
          element={
            <RequireAdmin>
              <AdminLayoutPage />
            </RequireAdmin>
          }
        >
          <Route index element={<Navigate to="general-settings" replace />} />
          <Route path="users" element={<AdminUsersPage />} />
          <Route path="groups" element={<AdminGroupsPage />} />
          <Route path="database-backup" element={<AdminDatabaseBackupPage />} />
          <Route
            path="storage-statistics"
            element={<AdminStorageStatisticsPage />}
          />
          <Route
            path="storage-settings"
            element={<AdminStorageSettingsPage />}
          />
          <Route
            path="general-settings"
            element={<AdminGeneralSettingsPage />}
          />
          <Route
            path="privacy-settings"
            element={<AdminPrivacySettingsPage />}
          />
          <Route path="security" element={<AdminSecurityDiagnosticsPage />} />
          <Route
            path="identity-providers"
            element={<AdminIdentityProvidersPage />}
          />
          <Route
            path="notifications-settings"
            element={<AdminNotificationsSettingsPage />}
          />
          <Route
            path="email-settings"
            element={<Navigate to="/admin/notifications-settings" replace />}
          />
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

      <Route
        path="/oauth/app-code/:id"
        element={
          <RequireAuth>
            <AppCodeApprovalPage />
          </RequireAuth>
        }
      />

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}
