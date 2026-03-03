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
} from "@mui/icons-material";
import { useAuth } from "../../../features/auth";
import { useTranslation } from "react-i18next";
import { useState, type MouseEvent } from "react";
import { useNavigate } from "react-router-dom";

export const UserMenu = () => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const { t } = useTranslation("common");
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

  const displayName = user?.displayName || user?.username || "User";
  const avatarLetter = displayName.charAt(0).toUpperCase();

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

        <MenuItem
          onClick={() => {
            handleClose();
            navigate("/profile");
          }}
        >
          <ListItemIcon>
            <Person fontSize="small" />
          </ListItemIcon>
          <ListItemText>{t("userMenu.profile")}</ListItemText>
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
