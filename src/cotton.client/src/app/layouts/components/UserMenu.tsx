import {
  Avatar,
  IconButton,
  Menu,
  MenuItem,
  ListItemIcon,
  ListItemText,
  Divider,
  Typography,
  Box,
} from "@mui/material";
import {
  Logout,
  Person,
  AdminPanelSettings,
  BugReport,
} from "@mui/icons-material";
import { UserRole, useAuth } from "../../../features/auth";
import { useTranslation } from "react-i18next";
import { useState, type MouseEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useServerSettings } from "../../../shared/store/useServerSettings";

export const UserMenu = () => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const { t } = useTranslation("common");
  const { data: serverSettings } = useServerSettings();
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const isOpen = Boolean(anchorEl);

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
    setAnchorEl(event.currentTarget);
  };

  const handleClose = () => {
    setAnchorEl(null);
  };

  const handleLogout = async () => {
    handleClose();
    await logout();
  };

  const detectBrowser = (): string => {
    const ua = navigator.userAgent;

    if (ua.includes("Edg/")) {
      return "Microsoft Edge";
    }
    if (ua.includes("OPR/") || ua.includes("Opera")) {
      return "Opera";
    }
    if (ua.includes("Firefox/")) {
      return "Firefox";
    }
    if (ua.includes("Chrome/")) {
      return "Chrome";
    }
    if (ua.includes("Safari/")) {
      return "Safari";
    }

    return "Unknown";
  };

  const detectOs = (): string => {
    const ua = navigator.userAgent;

    if (ua.includes("Windows NT")) {
      return "Windows";
    }
    if (ua.includes("Mac OS X")) {
      return "macOS";
    }
    if (ua.includes("Android")) {
      return "Android";
    }
    if (ua.includes("iPhone") || ua.includes("iPad")) {
      return "iOS";
    }
    if (ua.includes("Linux")) {
      return "Linux";
    }

    return "Unknown";
  };

  const openBugReport = () => {
    handleClose();

    const version = serverSettings?.version ?? "unknown";
    const browser = detectBrowser();
    const os = detectOs();
    const currentUrl = window.location.href;
    const openedAtUtc = new Date().toISOString();

    const url = new URL("https://github.com/bvdcode/cotton/issues/new");
    url.searchParams.set("labels", "bug");
    url.searchParams.set("assignees", "bvdcode");
    url.searchParams.set("title", "[Bug]: ");
    url.searchParams.set(
      "body",
      `## Version
${version}

## Description


## Steps to reproduce
1. 
2. 
3. 

## Expected behavior


## Actual behavior


## Environment
- Cotton version: ${version}
- Browser: ${browser}
- OS: ${os}
- Current URL: ${currentUrl}
- Opened at (UTC): ${openedAtUtc}
`,
    );

    window.open(url.toString(), "_blank", "noopener,noreferrer");
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
            bgcolor: "primary.main",
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
              minWidth: 200,
            },
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
              navigate("/admin/users");
            }}
          >
            <ListItemIcon>
              <AdminPanelSettings fontSize="small" />
            </ListItemIcon>
            <ListItemText>{t("userMenu.admin")}</ListItemText>
          </MenuItem>
        )}

        <MenuItem
          onClick={() => {
            openBugReport();
          }}
        >
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

        {serverSettings?.version && <Divider />}
        {serverSettings?.version && (
          <Box px={2}>
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ display: "block", textAlign: "center" }}
            >
              {serverSettings.version}
            </Typography>
          </Box>
        )}
      </Menu>
    </>
  );
};
