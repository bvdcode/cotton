import {
  Box,
  FormControl,
  List,
  ListItemButton,
  ListItemIcon,
  MenuItem,
  Paper,
  Select,
  Tooltip,
} from "@mui/material";
import type { SelectChangeEvent } from "@mui/material/Select";
import BackupIcon from "@mui/icons-material/Backup";
import GroupsIcon from "@mui/icons-material/Groups";
import NotificationsActiveIcon from "@mui/icons-material/NotificationsActive";
import PersonIcon from "@mui/icons-material/Person";
import QueryStatsIcon from "@mui/icons-material/QueryStats";
import SecurityIcon from "@mui/icons-material/Security";
import LoginIcon from "@mui/icons-material/Login";
import SettingsIcon from "@mui/icons-material/Settings";
import ShieldIcon from "@mui/icons-material/Shield";
import StorageIcon from "@mui/icons-material/Storage";
import type { ReactNode } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ADMIN_PAGE_SURFACE_WIDTH } from "./components/AdminPageSurface";

const ADMIN_NAV_WIDTH = 64;

type AdminMenuItem = {
  id:
    | "generalSettings"
    | "users"
    | "groups"
    | "privacySettings"
    | "securityDiagnostics"
    | "identityProviders"
    | "storageSettings"
    | "storageStatistics"
    | "notificationsSettings"
    | "databaseBackup";
  to: string;
  title: string;
  icon: ReactNode;
  activePaths?: string[];
};

export const AdminLayoutPage = () => {
  const { t } = useTranslation("admin");
  const navigate = useNavigate();
  const location = useLocation();

  const items: AdminMenuItem[] = [
    {
      id: "generalSettings",
      to: "/admin/general-settings",
      title: t("menu.generalSettings"),
      icon: <SettingsIcon />,
    },
    {
      id: "users",
      to: "/admin/users",
      title: t("menu.users"),
      icon: <PersonIcon />,
    },
    {
      id: "groups",
      to: "/admin/groups",
      title: t("menu.groups"),
      icon: <GroupsIcon />,
    },
    {
      id: "privacySettings",
      to: "/admin/privacy-settings",
      title: t("menu.privacySettings"),
      icon: <ShieldIcon />,
    },
    {
      id: "securityDiagnostics",
      to: "/admin/security",
      title: t("menu.securityDiagnostics"),
      icon: <SecurityIcon />,
    },
    {
      id: "notificationsSettings",
      to: "/admin/notifications-settings",
      title: t("menu.notificationsSettings"),
      icon: <NotificationsActiveIcon />,
    },
    {
      id: "identityProviders",
      to: "/admin/identity-providers",
      title: t("menu.identityProviders"),
      icon: <LoginIcon />,
    },
    {
      id: "storageSettings",
      to: "/admin/storage-settings",
      title: t("menu.storageSettings"),
      icon: <StorageIcon />,
    },
    {
      id: "storageStatistics",
      to: "/admin/storage-statistics",
      title: t("menu.storageStatistics"),
      icon: <QueryStatsIcon />,
    },
    {
      id: "databaseBackup",
      to: "/admin/database-backup",
      title: t("menu.databaseBackup"),
      icon: <BackupIcon />,
    },
  ];

  const isActive = (item: AdminMenuItem) =>
    (item.activePaths ?? [item.to]).some((path) =>
      location.pathname.startsWith(path),
    );

  const selectedTo: string =
    items.find((item) => isActive(item))?.to ?? items[0].to;

  const handleMobileNavigate = (event: SelectChangeEvent<string>) => {
    navigate(event.target.value);
  };

  return (
    <Box
      pt={{
        xs: 1,
        md: 3,
      }}
      pb={{
        xs: 0,
        md: 2,
      }}
      width="100%"
      display="flex"
      flexDirection="column"
      height="100%"
      minHeight={0}
    >
      <Paper sx={{ display: { xs: "block", md: "none" }, mb: 2 }}>
        <FormControl fullWidth size="small">
          <Select
            labelId="admin-menu-navigate-label"
            value={selectedTo}
            onChange={handleMobileNavigate}
          >
            {items.map((item) => (
              <MenuItem key={item.id} value={item.to}>
                {item.title}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Paper>

      <Box
        display="grid"
        flex={1}
        minHeight={0}
        sx={(theme) => ({
          width: {
            xs: "100%",
            md: `min(
              100%,
              calc(${ADMIN_NAV_WIDTH}px + ${theme.spacing(2)} + ${ADMIN_PAGE_SURFACE_WIDTH}px)
            )`,
          },
          mx: { md: "auto" },
          alignItems: "start",
          gridTemplateColumns: {
            xs: "minmax(0, 1fr)",
            md: `${ADMIN_NAV_WIDTH}px minmax(0, 1fr)`,
          },
          gap: 2,
        })}
      >
        <Paper
          sx={{
            display: { xs: "none", md: "flex" },
            flexDirection: "column",
            alignSelf: "start",
          }}
        >
          <List
            sx={{
              py: 1,
              display: "flex",
              flexDirection: "column",
              gap: 0.5,
            }}
          >
            {items.map((item) => (
              <Tooltip key={item.id} title={item.title} placement="right">
                <ListItemButton
                  aria-label={item.title}
                  selected={isActive(item)}
                  onClick={() => navigate(item.to)}
                  sx={{
                    justifyContent: "center",
                    minHeight: 44,
                    mx: 0.75,
                    px: 0.75,
                    borderRadius: 1,
                  }}
                >
                  <ListItemIcon
                    sx={{
                      minWidth: 0,
                      color: "inherit",
                      justifyContent: "center",
                    }}
                  >
                    {item.icon}
                  </ListItemIcon>
                </ListItemButton>
              </Tooltip>
            ))}
          </List>
        </Paper>

        <Box
          sx={{ overflowY: "auto", overflowX: "hidden" }}
          minHeight={0}
          minWidth={0}
          display="flex"
          flexDirection="column"
        >
          <Outlet />
        </Box>
      </Box>
    </Box>
  );
};
