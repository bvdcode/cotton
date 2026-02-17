import type { RouteConfig } from "./types";
import { RequireAdmin, RequireAuth, UserRole, useAuth } from "../features/auth";
import { Routes, Route, Navigate, useParams } from "react-router-dom";

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
  ProfilePage,
  TrashPage,
  SearchPage,
  SharePage,
  AdminLayoutPage,
  AdminUsersPage,
  ResetPasswordPage,
  VerifyEmailPage,
} from "../pages";
import { AppLayout, PublicLayout } from "./layouts";
import { Folder, Home, Person, Delete, Search, AdminPanelSettings } from "@mui/icons-material";
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
  const { user } = useAuth();
  const isAdmin = user?.role === UserRole.Admin;

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
    {
      path: "/profile",
      icon: <Person />,
      protected: true,
      translationKey: "profile",
      element: <ProfilePage />,
    },
  ];

  if (isAdmin) {
    appRoutes.push({
      path: "/admin",
      icon: <AdminPanelSettings />,
      protected: true,
      translationKey: "admin",
      element: <Navigate to="/admin/users" replace />,
    });
  }

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
        </Route>

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
