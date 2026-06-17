import {
  Avatar,
  Box,
  Divider,
  IconButton,
  LinearProgress,
  Link,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Typography,
} from "@mui/material";
import {
  AdminPanelSettings,
  BugReport,
  Logout,
  Person,
} from "@mui/icons-material";
import { useQuery } from "@tanstack/react-query";
import { useEffect, useState, type MouseEvent } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { UserRole, useAuth } from "../../../features/auth";
import { queryKeys } from "../../../shared/api/queries/queryKeys";
import { storageQuotaApi } from "../../../shared/api/storageQuotaApi";
import {
  useLocalPreferencesStore,
} from "../../../shared/store/localPreferencesStore";
import { useServerSettings } from "../../../shared/store/useServerSettings";
import { formatBytes } from "../../../shared/utils/formatBytes";
import {
  buildBugReportUrl,
  initializeBugReportConsoleCapture,
} from "./bugReportPrefill";
import { UserMenuAppDownloads } from "./UserMenuAppDownloads";

const STORAGE_QUOTA_STALE_TIME_MS = 60_000;
const storageQuotaProgressSx = {
  mt: 0.75,
  height: 4,
  borderRadius: 999,
  "& .MuiLinearProgress-bar": {
    transition: "none",
  },
};
const getStorageQuotaPercent = (
  usedBytes: number,
  quotaBytes: number | null,
): number | null => {
  if (!quotaBytes || quotaBytes <= 0) {
    // No quota configured (unlimited): hide the bar rather than render it full.
    return null;
  }

  return Math.min(100, Math.max(0, (usedBytes / quotaBytes) * 100));
};

const getStorageQuotaColor = (
  percent: number | null,
): "primary" | "warning" | "error" => {
  if (percent === null || percent >= 100) {
    return "primary";
  }

  if (percent >= 95) {
    return "error";
  }

  return percent >= 80 ? "warning" : "primary";
};

export const UserMenu = () => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const { t } = useTranslation("common");
  const { data: serverSettings } = useServerSettings();
  const recordDeveloperSettingsUnlockClick = useLocalPreferencesStore(
    (state) => state.recordDeveloperSettingsUnlockClick,
  );
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const [menuContentVisible, setMenuContentVisible] = useState(false);
  const isOpen = Boolean(anchorEl);
  const storageQuotaQuery = useQuery({
    queryKey: queryKeys.storageQuota.current(),
    queryFn: storageQuotaApi.getCurrent,
    enabled: Boolean(user),
    staleTime: STORAGE_QUOTA_STALE_TIME_MS,
  });

  useEffect(() => {
    initializeBugReportConsoleCapture();
  }, []);

  const getAvatarInitials = (args: {
    firstName?: string | null;
    lastName?: string | null;
    username?: string | null;
    email?: string | null;
    displayName?: string | null;
  }): string => {
    const first = (args.firstName ?? "").trim();
    const last = (args.lastName ?? "").trim();
    if (first && last) {
      return `${first.charAt(0)}${last.charAt(0)}`.toUpperCase();
    }

    const fallback = (
      args.displayName ??
      args.username ??
      args.email ??
      ""
    ).trim();
    if (!fallback) return "";

    const parts = fallback.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return `${parts[0].charAt(0)}${parts[1].charAt(0)}`.toUpperCase();
    }

    return fallback.slice(0, 2).toUpperCase();
  };

  const handleOpen = (event: MouseEvent<HTMLElement>) => {
    setMenuContentVisible(true);
    setAnchorEl(event.currentTarget);
  };

  const handleClose = () => {
    setAnchorEl(null);
  };

  const handleMenuExited = () => {
    setMenuContentVisible(false);
  };

  const handleLogout = async () => {
    handleClose();
    await logout();
  };

  const openBugReport = () => {
    handleClose();

    const reportUrl = buildBugReportUrl({
      serverVersion: serverSettings?.version,
      userRole: user?.role,
      currentUrl: window.location.href,
    });

    window.open(reportUrl, "_blank", "noopener,noreferrer");
  };

  const handleVersionClick = () => {
    recordDeveloperSettingsUnlockClick();
  };

  const fullName = [user?.firstName, user?.lastName]
    .filter(Boolean)
    .join(" ")
    .trim();
  const displayName =
    fullName ||
    user?.displayName ||
    user?.username ||
    user?.email ||
    t("userMenu.user");
  const caption = user?.username ? `@${user.username}` : user?.email || "";
  const avatarInitials = getAvatarInitials({
    firstName: user?.firstName,
    lastName: user?.lastName,
    username: user?.username,
    email: user?.email,
    displayName,
  });
  const isAdmin = user?.role === UserRole.Admin;
  const quota = storageQuotaQuery.data;
  const storageQuotaPercent = quota
    ? getStorageQuotaPercent(quota.usedBytes, quota.quotaBytes)
    : null;
  const storageQuotaColor = getStorageQuotaColor(storageQuotaPercent);
  const storageQuotaText = quota
    ? quota.quotaBytes && quota.quotaBytes > 0
      ? t("userMenu.storageQuota.usedOfLimit", {
          used: formatBytes(quota.usedBytes),
          quota: formatBytes(quota.quotaBytes),
        })
      : t("userMenu.storageQuota.usedNoLimit", {
          used: formatBytes(quota.usedBytes),
        })
    : t("userMenu.storageQuota.loading");
  const showStorageQuota =
    menuContentVisible && (storageQuotaQuery.isPending || Boolean(quota));

  return (
    <>
      <IconButton
        onClick={handleOpen}
        size="small"
        aria-controls={isOpen ? "user-menu" : undefined}
        aria-haspopup="true"
        aria-expanded={isOpen ? "true" : undefined}
        sx={{
          padding: 0,
        }}
      >
        <Avatar
          alt={displayName}
          src={user?.pictureUrl}
          sx={{
            width: 40,
            height: 40,
            bgcolor: "background.paper",
            color: "primary.main",
            fontWeight: 600,
          }}
        >
          {!user?.pictureUrl && avatarInitials}
        </Avatar>
      </IconButton>

      <Menu
        id="user-menu"
        anchorEl={anchorEl}
        open={isOpen}
        onClose={handleClose}
        transformOrigin={{ horizontal: "right", vertical: "top" }}
        anchorOrigin={{ horizontal: "right", vertical: "bottom" }}
        slotProps={{
          paper: {
            elevation: 3,
            sx: {
              mt: 1.5,
              minWidth: 240,
            },
          },
          transition: {
            onExited: handleMenuExited,
          },
        }}
      >
        <Box px={2} py={1.5}>
          <Typography variant="subtitle2" noWrap>
            {displayName}
          </Typography>
          {caption && caption !== displayName && (
            <Typography variant="body2" color="text.primary" noWrap>
              {caption}
            </Typography>
          )}
        </Box>

        <Divider />

        <MenuItem
          onClick={() => {
            handleClose();
            navigate("/settings");
          }}
        >
          <ListItemIcon>
            <Person fontSize="small" />
          </ListItemIcon>
          <ListItemText>{t("userMenu.profile")}</ListItemText>
        </MenuItem>

        {isAdmin && (
          <MenuItem
            onClick={() => {
              handleClose();
              navigate("/admin");
            }}
          >
            <ListItemIcon>
              <AdminPanelSettings fontSize="small" />
            </ListItemIcon>
            <ListItemText>{t("userMenu.admin")}</ListItemText>
          </MenuItem>
        )}

        <MenuItem onClick={openBugReport}>
          <ListItemIcon>
            <BugReport fontSize="small" />
          </ListItemIcon>
          <ListItemText>{t("userMenu.help")}</ListItemText>
        </MenuItem>

        <MenuItem onClick={handleLogout}>
          <ListItemIcon>
            <Logout fontSize="small" />
          </ListItemIcon>
          <ListItemText>{t("userMenu.logout")}</ListItemText>
        </MenuItem>

        {showStorageQuota && <Divider />}
        {showStorageQuota && (
          <Box px={2} py={1.25}>
            <Typography variant="caption" color="text.secondary" noWrap>
              {storageQuotaText}
            </Typography>
            {storageQuotaPercent !== null ? (
              <LinearProgress
                variant="determinate"
                value={storageQuotaPercent}
                color={storageQuotaColor}
                aria-label={t("userMenu.storageQuota.label")}
                sx={storageQuotaProgressSx}
              />
            ) : storageQuotaQuery.isPending ? (
              <LinearProgress
                aria-label={t("userMenu.storageQuota.loading")}
                sx={storageQuotaProgressSx}
              />
            ) : null}
          </Box>
        )}

        <Divider />
        <Box
          px={2}
          py={0.5}
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: serverSettings?.version ? "space-between" : "center",
            gap: 1.5,
          }}
        >
          <UserMenuAppDownloads onOpenLink={handleClose} />
          {serverSettings?.version && (
            <Link
              href="https://cottoncloud.dev"
              underline="none"
              onClick={handleVersionClick}
              sx={{
                display: "block",
                py: 0.25,
                color: "text.secondary",
                textAlign: "center",
                whiteSpace: "nowrap",
                "&:hover": {
                  color: "primary.main",
                },
              }}
            >
              <Typography
                component="span"
                variant="caption"
                sx={{ fontSize: "0.6875rem", lineHeight: 1.2 }}
              >
                {serverSettings.version}
              </Typography>
            </Link>
          )}
        </Box>
      </Menu>
    </>
  );
};
