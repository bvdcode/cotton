import type { RouteConfig } from "./types";
import { RequireAdmin, RequireAuth, useAuth } from "../features/auth";
import { lazy, Suspense, useEffect, useState, type JSX } from "react";
import {
  Routes,
  Route,
  Navigate,
  useParams,
  useLocation,
  matchPath,
} from "react-router-dom";
import Loader from "../shared/ui/Loader";
import { useTranslation } from "react-i18next";
import { AppLayout, PublicLayout } from "./layouts";
import { Folder, Home, Delete } from "@mui/icons-material";
import { SetupGate } from "../features/settings/SetupGate";
import { unlockApi, type UnlockStatusResponse } from "../shared/api/unlockApi";

const FilesPage = lazy(() =>
  import("../pages/files").then((module) => ({ default: module.FilesPage })),
);
const HomePage = lazy(() =>
  import("../pages/home").then((module) => ({ default: module.HomePage })),
);
const LoginPage = lazy(() =>
  import("../pages/login/LoginPage").then((module) => ({
    default: module.LoginPage,
  })),
);
const NotFoundPage = lazy(() =>
  import("../pages/not-found/NotFoundPage").then((module) => ({
    default: module.NotFoundPage,
  })),
);
const OnboardingPage = lazy(() =>
  import("../pages/onboarding/OnboardingPage").then((module) => ({
    default: module.OnboardingPage,
  })),
);
const SettingsPage = lazy(() =>
  import("../pages/profile").then((module) => ({
    default: module.SettingsPage,
  })),
);
const TrashPage = lazy(() =>
  import("../pages/trash").then((module) => ({ default: module.TrashPage })),
);
const SearchPage = lazy(() =>
  import("../pages/search/SearchPage").then((module) => ({
    default: module.SearchPage,
  })),
);
const SharePage = lazy(() =>
  import("../pages/share/SharePage").then((module) => ({
    default: module.SharePage,
  })),
);
const AdminLayoutPage = lazy(() =>
  import("../pages/admin/AdminLayoutPage").then((module) => ({
    default: module.AdminLayoutPage,
  })),
);
const AdminUsersPage = lazy(() =>
  import("../pages/admin/users/AdminUsersPage").then((module) => ({
    default: module.AdminUsersPage,
  })),
);
const AdminGroupsPage = lazy(() =>
  import("../pages/admin/groups/AdminGroupsPage").then((module) => ({
    default: module.AdminGroupsPage,
  })),
);
const AdminDatabaseBackupPage = lazy(() =>
  import("../pages/admin/database-backup/AdminDatabaseBackupPage").then(
    (module) => ({
      default: module.AdminDatabaseBackupPage,
    }),
  ),
);
const AdminStorageStatisticsPage = lazy(() =>
  import("../pages/admin/storage-statistics/AdminStorageStatisticsPage").then(
    (module) => ({
      default: module.AdminStorageStatisticsPage,
    }),
  ),
);
const AdminStorageSettingsPage = lazy(() =>
  import("../pages/admin/settings/AdminStorageSettingsPage").then((module) => ({
    default: module.AdminStorageSettingsPage,
  })),
);
const AdminGeneralSettingsPage = lazy(() =>
  import("../pages/admin/settings/AdminGeneralSettingsPage").then((module) => ({
    default: module.AdminGeneralSettingsPage,
  })),
);
const AdminPrivacySettingsPage = lazy(() =>
  import("../pages/admin/settings/AdminPrivacySettingsPage").then((module) => ({
    default: module.AdminPrivacySettingsPage,
  })),
);
const AdminSecurityDiagnosticsPage = lazy(() =>
  import("../pages/admin/security/AdminSecurityDiagnosticsPage").then(
    (module) => ({
      default: module.AdminSecurityDiagnosticsPage,
    }),
  ),
);
const AdminIdentityProvidersPage = lazy(() =>
  import("../pages/admin/identity-providers/AdminIdentityProvidersPage").then(
    (module) => ({
      default: module.AdminIdentityProvidersPage,
    }),
  ),
);
const AdminNotificationsSettingsPage = lazy(() =>
  import("../pages/admin/settings/AdminNotificationsSettingsPage").then(
    (module) => ({
      default: module.AdminNotificationsSettingsPage,
    }),
  ),
);
const ResetPasswordPage = lazy(() =>
  import("../pages/reset-password/ResetPasswordPage").then((module) => ({
    default: module.ResetPasswordPage,
  })),
);
const VerifyEmailPage = lazy(() =>
  import("../pages/verify-email/VerifyEmailPage").then((module) => ({
    default: module.VerifyEmailPage,
  })),
);
const AppCodeApprovalPage = lazy(() =>
  import("../pages/oauth/AppCodeApprovalPage").then((module) => ({
    default: module.AppCodeApprovalPage,
  })),
);
const UnlockPage = lazy(() =>
  import("../pages/unlock/UnlockPage").then((module) => ({
    default: module.UnlockPage,
  })),
);
const SetupWizardPage = lazy(() =>
  import("../pages/setup/SetupWizardPage").then((module) => ({
    default: module.SetupWizardPage,
  })),
);

const withRouteSuspense = (element: JSX.Element) => (
  <Suspense fallback={<Loader />}>{element}</Suspense>
);

const RedirectSToShare = () => {
  const { token } = useParams<{ token: string }>();
  return <Navigate to={`/share/${token ?? ""}`} replace />;
};

const publicRoutes: RouteConfig[] = [
  {
    path: "/unlock",
    element: withRouteSuspense(<UnlockPage />),
  },
  {
    path: "/login",
    element: withRouteSuspense(<LoginPage />),
  },
  {
    path: "/s/:token",
    element: <RedirectSToShare />,
  },
  {
    path: "/share/:token",
    element: withRouteSuspense(<SharePage />),
  },
  {
    path: "/reset-password",
    element: withRouteSuspense(<ResetPasswordPage />),
  },
  {
    path: "/verify-email",
    element: withRouteSuspense(<VerifyEmailPage />),
  },
];

export function AppRoutes() {
  const { t } = useTranslation(["login", "unlock"]);
  const location = useLocation();
  const [lockStatus, setLockStatus] = useState<UnlockStatusResponse | null>(null);
  const [lockCheckState, setLockCheckState] = useState<
    "checking" | "locked" | "unlocked"
  >("checking");
  const {
    hydrated,
    isInitializing,
    isAuthenticated,
    refreshEnabled,
    hasChecked,
    ensureAuth,
  } = useAuth();

  useEffect(() => {
    let cancelled = false;

    unlockApi
      .getStatus()
      .then((status) => {
        if (cancelled) return;
        setLockStatus(status);
        setLockCheckState(status ? "locked" : "unlocked");
      })
      .catch(() => {
        if (cancelled) return;
        setLockStatus(null);
        setLockCheckState("unlocked");
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const isPublicRoute = publicRoutes.some((route) =>
    Boolean(
      matchPath(
        { path: route.path, end: true },
        location.pathname,
      ),
    ),
  );

  useEffect(() => {
    if (lockCheckState !== "unlocked") return;
    if (isPublicRoute) return;
    ensureAuth();
  }, [ensureAuth, isPublicRoute, lockCheckState]);

  if (lockCheckState === "checking") {
    return (
      <Loader
        overlay={true}
        title={t("checking.title", { ns: "unlock" })}
        caption={t("checking.caption", { ns: "unlock" })}
      />
    );
  }

  if (lockCheckState === "locked") {
    return (
      <Routes>
        <Route element={<PublicLayout />}>
          <Route path="/unlock" element={withRouteSuspense(<UnlockPage initialStatus={lockStatus ?? undefined} />)} />
          <Route
            path="*"
            element={
              <Navigate
                to="/unlock"
                replace
                state={{ from: location.pathname, status: lockStatus }}
              />
            }
          />
        </Route>
      </Routes>
    );
  }

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
      element: withRouteSuspense(<HomePage />),
    },
    {
      path: "/files",
      icon: <Folder />,
      protected: true,
      translationKey: "files",
      element: withRouteSuspense(<FilesPage />),
    },
    {
      path: "/trash",
      icon: <Delete />,
      protected: true,
      translationKey: "trash",
      element: withRouteSuspense(<TrashPage />),
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

        <Route path="/search" element={withRouteSuspense(<SearchPage />)} />

        <Route
          path="/admin"
          element={
            <RequireAdmin>
              {withRouteSuspense(<AdminLayoutPage />)}
            </RequireAdmin>
          }
        >
          <Route index element={<Navigate to="general-settings" replace />} />
          <Route
            path="users"
            element={<AdminUsersPage />}
          />
          <Route
            path="groups"
            element={<AdminGroupsPage />}
          />
          <Route
            path="database-backup"
            element={<AdminDatabaseBackupPage />}
          />
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
          <Route
            path="security"
            element={<AdminSecurityDiagnosticsPage />}
          />
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
        <Route path="/settings" element={withRouteSuspense(<SettingsPage />)} />
        <Route path="/profile" element={<Navigate to="/settings" replace />} />

        {/* Deep link into a specific folder by node id */}
        <Route
          path="/files/:nodeId"
          element={withRouteSuspense(<FilesPage />)}
        />
        <Route
          path="/trash/:nodeId"
          element={withRouteSuspense(<TrashPage />)}
        />
      </Route>

      <Route
        path="/setup"
        element={
          <RequireAuth>
            <SetupGate>
              {withRouteSuspense(<SetupWizardPage />)}
            </SetupGate>
          </RequireAuth>
        }
      />

      <Route
        path="/onboarding"
        element={
          <RequireAuth>
            {withRouteSuspense(<OnboardingPage />)}
          </RequireAuth>
        }
      />

      <Route
        path="/oauth/app-code/:id"
        element={
          <RequireAuth>
            {withRouteSuspense(<AppCodeApprovalPage />)}
          </RequireAuth>
        }
      />

      <Route path="*" element={withRouteSuspense(<NotFoundPage />)} />
    </Routes>
  );
}
