import { useEffect, useState } from "react";
import { Avatar, Box, Divider, IconButton, List, ListItemButton, ListItemIcon, ListItemText, Popover, Typography } from "@mui/material";
import { DarkMode, LightMode, Logout } from "@mui/icons-material";
import { useAuthStore } from "../store/authStore";
import { useThemeStore } from "../store/themeStore";
import { useTranslation } from "react-i18next";
import LanguageSwitcher from "./LanguageSwitcher";

export default function UserMenu() {
  const { t } = useTranslation();
  const logout = useAuthStore((s) => s.logout);
  const apiService = useAuthStore((s) => s.apiService);
  const mode = useThemeStore((s) => s.mode);
  const toggleTheme = useThemeStore((s) => s.toggleTheme);

  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const [userInfo, setUserInfo] = useState<{ displayName: string; username: string; avatarUrl?: string } | null>(null);

  useEffect(() => {
    const fetchUserInfo = async () => {
      if (apiService) {
        try {
          const info = await apiService.getUserInfo();
          setUserInfo(info);
        } catch (error) {
          console.error("Failed to fetch user info:", error);
        }
      }
    };
    fetchUserInfo();
  }, [apiService]);

  const handleOpen = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const handleClose = () => setAnchorEl(null);

  const handleLogout = async () => {
    handleClose();
    await logout();
  };

  const open = Boolean(anchorEl);

  return (
    <Box display="flex" alignItems="center" justifyContent="flex-end">
      <IconButton onClick={handleOpen} sx={{ p: 0 }}>
        <Avatar src={userInfo?.avatarUrl} alt={userInfo?.displayName || "User"}>
          {userInfo?.displayName?.[0]?.toUpperCase() || "U"}
        </Avatar>
      </IconButton>
      <Popover
        open={open}
        anchorEl={anchorEl}
        onClose={handleClose}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
        slotProps={{ paper: { sx: { mt: 1 } } }}
      >
        <Box sx={{ p: 2, minWidth: 250 }}>
          <Typography variant="subtitle1" fontWeight="bold">
            {userInfo?.displayName || "User"}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {!userInfo?.username.includes("@") && "@"}
            {userInfo?.username || "username"}
          </Typography>
        </Box>
        <Divider />
        <List dense>
          <ListItemButton onClick={toggleTheme}>
            <ListItemIcon>{mode === "dark" ? <LightMode /> : <DarkMode />}</ListItemIcon>
            <ListItemText
              primary={mode === "dark" ? t("navigation.lightMode") : t("navigation.darkMode")}
            />
          </ListItemButton>
          <LanguageSwitcher />
          <Divider />
          <ListItemButton onClick={handleLogout}>
            <ListItemIcon><Logout /></ListItemIcon>
            <ListItemText primary={t("navigation.logout")} />
          </ListItemButton>
        </List>
      </Popover>
    </Box>
  );
}
