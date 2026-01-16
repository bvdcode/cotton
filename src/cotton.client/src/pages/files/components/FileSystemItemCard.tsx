import { Box, IconButton, Typography } from "@mui/material";
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
  const [actionsOpen, setActionsOpen] = useState(false);

  const handleToggleActions = (e: MouseEvent<HTMLButtonElement>) => {
    e.stopPropagation();
    setActionsOpen(!actionsOpen);
  };

  const handleActionClick =
    (action: FileSystemItemCardAction) =>
    (e: MouseEvent<HTMLButtonElement>) => {
      e.stopPropagation();
      setActionsOpen(false);
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
            onClick={handleToggleActions}
            className="card-menu-button"
            sx={{
              p: 0.5,
              width: 28,
              height: 28,
              opacity: actionsOpen ? 1 : 0,
              transition: "opacity 0.2s, transform 0.3s",
              transform: actionsOpen ? "rotate(90deg)" : "rotate(0deg)",
              "& svg": {
                fontSize: "1rem",
              },
            }}
          >
            <MoreVert />
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

      {actions && actions.length > 0 && actionsOpen && (
        <Box
          sx={{
            position: "absolute",
            bottom: subtitle ? 56 : 36,
            right: 4,
            display: "flex",
            flexDirection: "column",
            gap: 0.75,
            animation: "slideUp 0.2s ease-out",
            "@keyframes slideUp": {
              from: {
                opacity: 0,
                transform: "translateY(10px)",
              },
              to: {
                opacity: 1,
                transform: "translateY(0)",
              },
            },
          }}
          onClick={(e) => e.stopPropagation()}
        >
          {actions.map((action, idx) => (
            <IconButton
              key={idx}
              size="small"
              onClick={handleActionClick(action)}
              title={action.tooltip}
              sx={{
                p: 0.5,
                width: 28,
                height: 28,
                "& svg": {
                  fontSize: "1rem",
                },
              }}
            >
              {action.icon}
            </IconButton>
          ))}
        </Box>
      )}
    </Box>
  );
};
