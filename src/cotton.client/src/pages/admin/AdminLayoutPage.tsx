import {
  Box,
  FormControl,
  InputLabel,
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
import EmailIcon from "@mui/icons-material/Email";
import GroupsIcon from "@mui/icons-material/Groups";
import SettingsIcon from "@mui/icons-material/Settings";
import StorageIcon from "@mui/icons-material/Storage";
import type { ReactNode } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

type AdminMenuItem = {
  id:
    | "users"
    | "generalSettings"
    | "storage"
    | "emailSettings"
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
      id: "users",
      to: "/admin/users",
      title: t("menu.usersAndGroups", {
        defaultValue: "Users and groups",
      }),
      icon: <GroupsIcon />,
    },
    {
      id: "generalSettings",
      to: "/admin/general-settings",
      title: t("menu.generalSettings"),
      icon: <SettingsIcon />,
    },
    {
      id: "emailSettings",
      to: "/admin/email-settings",
      title: t("menu.emailSettings"),
      icon: <EmailIcon />,
    },
    {
      id: "databaseBackup",
      to: "/admin/database-backup",
      title: t("menu.databaseBackup"),
      icon: <BackupIcon />,
    },
    {
      id: "storage",
      to: "/admin/storage-statistics",
      title: t("menu.storageAndStatistics", {
        defaultValue: t("menu.storageStatistics"),
      }),
      icon: <StorageIcon />,
      activePaths: ["/admin/storage-statistics", "/admin/storage-settings"],
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
      <Paper sx={{ display: { xs: "block", md: "none" }, p: 2, mb: 2 }}>
        <FormControl fullWidth size="small">
          <InputLabel id="admin-menu-navigate-label">
            {t("menu.navigate")}
          </InputLabel>
          <Select
            labelId="admin-menu-navigate-label"
            value={selectedTo}
            label={t("menu.navigate")}
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
        sx={{
          gridTemplateColumns: { xs: "1fr", md: "64px minmax(0, 1fr)" },
          gap: 2,
        }}
      >
        <Paper
          sx={{
            display: { xs: "none", md: "flex" },
            flexDirection: "column",
            minHeight: 0,
            height: "100%",
          }}
        >
          <List
            sx={{
              pt: 1,
              overflowY: "auto",
              minHeight: 0,
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
          display="flex"
          flexDirection="column"
        >
          <Outlet />
        </Box>
      </Box>
    </Box>
  );
};
