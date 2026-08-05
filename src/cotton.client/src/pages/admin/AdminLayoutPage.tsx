import BackupIcon from "@mui/icons-material/Backup";
import HealthAndSafetyIcon from "@mui/icons-material/HealthAndSafety";
import LoginIcon from "@mui/icons-material/Login";
import NotificationsActiveIcon from "@mui/icons-material/NotificationsActive";
import PersonIcon from "@mui/icons-material/Person";
import PolicyIcon from "@mui/icons-material/Policy";
import QueryStatsIcon from "@mui/icons-material/QueryStats";
import SettingsIcon from "@mui/icons-material/Settings";
import StorageIcon from "@mui/icons-material/Storage";
import { Box, Skeleton, Stack, useMediaQuery } from "@mui/material";
import type { Theme } from "@mui/material/styles";
import type { SelectChangeEvent } from "@mui/material/Select";
import { Suspense } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  selectAdminNavigationExpanded,
  useLocalPreferencesStore,
} from "@shared/store/localPreferencesStore";
import { ADMIN_PAGE_SURFACE_WIDTH } from "./components/AdminPageSurface";
import {
  DesktopAdminNavigation,
  MobileAdminNavigation,
} from "./components/AdminNavigation";
import {
  ADMIN_NAV_WIDTH,
  type AdminMenuSection,
} from "./components/adminNavigationModel";

const AdminContentSkeleton = () => (
  <Box width="100%">
    <Skeleton variant="text" width={240} height={40} sx={{ mb: 3 }} />
    <Stack spacing={3}>
      {[0, 1, 2].map((index) => (
        <Box key={index}>
          <Skeleton variant="text" width={180} height={24} />
          <Skeleton variant="text" width={320} height={18} sx={{ mb: 1 }} />
          <Skeleton variant="rounded" height={48} />
        </Box>
      ))}
    </Stack>
  </Box>
);

export const AdminLayoutPage = () => {
  const { t } = useTranslation("admin");
  const navigate = useNavigate();
  const location = useLocation();
  const defaultExpanded = useMediaQuery((theme: Theme) =>
    theme.breakpoints.up("lg"),
  );
  const storedExpanded = useLocalPreferencesStore(
    selectAdminNavigationExpanded,
  );
  const setExpanded = useLocalPreferencesStore(
    (state) => state.setAdminNavigationExpanded,
  );
  const expanded = storedExpanded ?? defaultExpanded;
  const navWidth = expanded
    ? ADMIN_NAV_WIDTH.expanded
    : ADMIN_NAV_WIDTH.collapsed;

  const sections: AdminMenuSection[] = [
    {
      id: "people",
      title: t("menu.sections.people"),
      items: [
        {
          id: "users",
          to: "/admin/users",
          title: t("menu.users"),
          icon: PersonIcon,
        },
        {
          id: "identityProviders",
          to: "/admin/identity-providers",
          title: t("menu.identityProviders"),
          icon: LoginIcon,
        },
      ],
    },
    {
      id: "storage",
      title: t("menu.sections.storage"),
      items: [
        {
          id: "storageSettings",
          to: "/admin/storage-settings",
          title: t("menu.storageSettings"),
          icon: StorageIcon,
        },
        {
          id: "storageStatistics",
          to: "/admin/storage-statistics",
          title: t("menu.storageStatistics"),
          icon: QueryStatsIcon,
        },
      ],
    },
    {
      id: "security",
      title: t("menu.sections.security"),
      items: [
        {
          id: "privacySettings",
          to: "/admin/privacy-settings",
          title: t("menu.privacySettings"),
          icon: PolicyIcon,
        },
        {
          id: "securityDiagnostics",
          to: "/admin/security",
          title: t("menu.securityDiagnostics"),
          icon: HealthAndSafetyIcon,
        },
      ],
    },
    {
      id: "system",
      title: t("menu.sections.system"),
      items: [
        {
          id: "generalSettings",
          to: "/admin/general-settings",
          title: t("menu.generalSettings"),
          icon: SettingsIcon,
        },
        {
          id: "notificationsSettings",
          to: "/admin/notifications-settings",
          title: t("menu.notificationsSettings"),
          icon: NotificationsActiveIcon,
        },
        {
          id: "databaseBackup",
          to: "/admin/database-backup",
          title: t("menu.databaseBackup"),
          icon: BackupIcon,
        },
      ],
    },
  ];
  const items = sections.flatMap((section) => section.items);
  const selectedTo =
    items.find((item) => location.pathname.startsWith(item.to))?.to ?? "";

  const handleMobileNavigate = (event: SelectChangeEvent<string>) => {
    navigate(event.target.value);
  };

  return (
    <Box
      pt={{ xs: 1, md: 3 }}
      pb={{ xs: 0, md: 2 }}
      width="100%"
      display="flex"
      flexDirection="column"
      height="100%"
      minHeight={0}
    >
      <MobileAdminNavigation
        sections={sections}
        selectedTo={selectedTo}
        label={t("menu.navigate")}
        onChange={handleMobileNavigate}
      />

      <Box
        display="grid"
        flex={1}
        minHeight={0}
        sx={(theme) => ({
          width: {
            xs: "100%",
            md: `min(
              100%,
              calc(${navWidth}px + ${theme.spacing(2)} + ${ADMIN_PAGE_SURFACE_WIDTH}px)
            )`,
          },
          mx: { md: "auto" },
          gridTemplateColumns: {
            xs: "minmax(0, 1fr)",
            md: `${navWidth}px minmax(0, 1fr)`,
          },
          gap: 2,
          transition: theme.transitions.create("grid-template-columns"),
        })}
      >
        <DesktopAdminNavigation
          sections={sections}
          expanded={expanded}
          onToggle={() => setExpanded(!expanded)}
          navigationLabel={t("title")}
          toggleLabel={t(expanded ? "menu.collapse" : "menu.expand")}
        />

        <Box
          sx={{ overflowY: "auto", overflowX: "hidden" }}
          minHeight={0}
          minWidth={0}
          display="flex"
          flexDirection="column"
          alignSelf="stretch"
        >
          <Suspense fallback={<AdminContentSkeleton />}>
            <Outlet />
          </Suspense>
        </Box>
      </Box>
    </Box>
  );
};
