import { Box, IconButton, Menu, MenuItem, Typography } from "@mui/material";
import { MoreVert } from "@mui/icons-material";
import type { SxProps, Theme } from "@mui/material/styles";
import type { ReactNode, MouseEvent } from "react";
import { useState } from "react";

export interface FileSystemItemCardAction {
  icon: ReactNode;
  onClick: () => void;
  tooltip?: string;
}

export interface FileSystemItemCardProps {
  icon: ReactNode;
  title: string;
  subtitle?: string;
  onClick?: () => void;
  actions?: FileSystemItemCardAction[];
  sx?: SxProps<Theme>;
}

export const FileSystemItemCard = ({
  icon,
  title,
  subtitle,
  onClick,
  actions,
  sx,
}: FileSystemItemCardProps) => {
  const clickable = typeof onClick === "function";
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const menuOpen = Boolean(anchorEl);

  const handleMenuClick = (e: MouseEvent<HTMLButtonElement>) => {
    e.stopPropagation();
    setAnchorEl(e.currentTarget);
  };

  const handleMenuClose = (e?: MouseEvent) => {
    if (e) e.stopPropagation();
    setAnchorEl(null);
  };

  const handleActionClick = (action: FileSystemItemCardAction) => (e: MouseEvent) => {
    e.stopPropagation();
    handleMenuClose();
    action.onClick();
  };

  return (
    <Box
      role={clickable ? "button" : undefined}
      tabIndex={clickable ? 0 : undefined}
      onClick={onClick}
      onKeyDown={(e) => {
        if (!clickable) return;
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onClick?.();
        }
      }}
      sx={{
        position: "relative",
        border: "1px solid",
        borderColor: "divider",
        borderRadius: 2,
        p: {
          xs: 1,
          sm: 1.25,
          md: 1,
        },
        cursor: clickable ? "pointer" : "default",
        userSelect: "none",
        outline: "none",
        "&:hover": clickable ? { bgcolor: "action.hover" } : undefined,
        "&:hover .card-menu-button": {
          opacity: 1,
        },
        "&:focus-visible": clickable
          ? {
              boxShadow: (theme) => `0 0 0 2px ${theme.palette.primary.main}`,
            }
          : undefined,
        ...sx,
      }}
    >
      <Box
        sx={{
          width: "100%",
          aspectRatio: "1 / 1",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          borderRadius: 1.5,
          overflow: "hidden",
          mb: 0.75,
          "& > svg": {
            width: "70%",
            height: "70%",
          },
        }}
      >
        {icon}
      </Box>

      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
        <Typography
          variant="body2"
          noWrap
          title={title}
          fontWeight={500}
          sx={{ flex: 1, fontSize: { xs: "0.8rem", md: "0.85rem" } }}
        >
          {title}
        </Typography>

        {actions && actions.length > 0 && (
          <IconButton
            size="small"
            onClick={handleMenuClick}
            className="card-menu-button"
            sx={{
              p: 0.25,
              opacity: menuOpen ? 1 : 0,
              transition: "opacity 0.2s",
            }}
          >
            <MoreVert sx={{ fontSize: "1rem" }} />
          </IconButton>
        )}
      </Box>

      {subtitle && (
        <Typography
          variant="caption"
          color="text.secondary"
          display="block"
          noWrap
          title={subtitle}
          sx={{ fontSize: { xs: "0.7rem", md: "0.75rem" } }}
        >
          {subtitle}
        </Typography>
      )}

      {actions && actions.length > 0 && (
        <Menu
          anchorEl={anchorEl}
          open={menuOpen}
          onClose={() => handleMenuClose()}
          onClick={(e) => e.stopPropagation()}
          anchorOrigin={{ vertical: "top", horizontal: "right" }}
          transformOrigin={{ vertical: "top", horizontal: "right" }}
        >
          {actions.map((action, idx) => (
            <MenuItem key={idx} onClick={handleActionClick(action)}>
              <Box sx={{ mr: 1, display: "flex" }}>{action.icon}</Box>
              {action.tooltip}
            </MenuItem>
          ))}
        </Menu>
      )}
    </Box>
  );
};
