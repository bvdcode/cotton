import { useState, type MouseEvent, useMemo } from "react";
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
  Language,
  Brightness4,
  Brightness7,
} from "@mui/icons-material";
import { useAuth } from "../../../features/auth";
import { useTheme } from "../../providers";
import { useTranslation } from "react-i18next";

export const UserMenu = () => {
  const { user, logout } = useAuth();
  const { resolvedMode, setTheme } = useTheme();
  const { i18n, t } = useTranslation("common");
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const isOpen = Boolean(anchorEl);

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

  const handleToggleTheme = () => {
    // Toggle based on effective theme, not raw mode, so system works intuitively
    const next = resolvedMode === "dark" ? "light" : "dark";
    setTheme(next);
  };

  const handleToggleLanguage = () => {
    const next = i18n.language === "ru" ? "en" : "ru";
    i18n.changeLanguage(next);
  };

  const displayName = user?.displayName || user?.username || "User";
  const avatarLetter = displayName.charAt(0).toUpperCase();
  const themeLabel = useMemo(
    () => (resolvedMode === "dark" ? t("userMenu.darkMode") : t("userMenu.lightMode")),
    [resolvedMode, t],
  );
  const ThemeIcon = resolvedMode === "dark" ? Brightness7 : Brightness4;

  return (
    <>
      <IconButton
        onClick={handleOpen}
        size="small"
        aria-controls={isOpen ? "user-menu" : undefined}
        aria-haspopup="true"
        aria-expanded={isOpen ? "true" : undefined}
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
          {!user?.pictureUrl && avatarLetter}
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
        <Box sx={{ px: 2, py: 1.5 }}>
          <Typography variant="subtitle2" noWrap>
            {displayName}
          </Typography>
          {user?.username && user.username !== displayName && (
            <Typography variant="body2" color="text.secondary" noWrap>
              @{user.username}
            </Typography>
          )}
        </Box>

        <Divider />

        <MenuItem onClick={handleToggleTheme}>
          <ListItemIcon>
            <ThemeIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText>{themeLabel}</ListItemText>
        </MenuItem>

        <MenuItem onClick={handleToggleLanguage}>
          <ListItemIcon>
            <Language fontSize="small" />
          </ListItemIcon>
          <ListItemText>{t("userMenu.changeLanguage")}</ListItemText>
        </MenuItem>

        <MenuItem onClick={handleLogout}>
          <ListItemIcon>
            <Logout fontSize="small" />
          </ListItemIcon>
          <ListItemText>{t("userMenu.logout")}</ListItemText>
        </MenuItem>
      </Menu>
    </>
  );
};
