import { Box, IconButton, Typography } from "@mui/material";
import type { SxProps, Theme } from "@mui/material/styles";
import type { ReactNode, MouseEvent } from "react";

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

  const handleActionClick = (action: FileSystemItemCardAction) => (e: MouseEvent<HTMLButtonElement>) => {
    e.stopPropagation();
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
        "&:focus-visible": clickable
          ? {
              boxShadow: (theme) => `0 0 0 2px ${theme.palette.primary.main}`,
            }
          : undefined,
        ...sx,
      }}
    >
      {actions && actions.length > 0 && (
        <Box
          sx={{
            position: "absolute",
            top: 4,
            right: 4,
            display: "flex",
            gap: 0.25,
            opacity: 0,
            transition: "opacity 0.2s",
            ".MuiBox-root:hover &": {
              opacity: 1,
            },
          }}
        >
          {actions.map((action, idx) => (
            <IconButton
              key={idx}
              size="small"
              onClick={handleActionClick(action)}
              title={action.tooltip}
              sx={{
                p: 0.5,
                bgcolor: "background.paper",
                boxShadow: 1,
                "&:hover": {
                  bgcolor: "action.hover",
                },
              }}
            >
              {action.icon}
            </IconButton>
          ))}
        </Box>
      )}

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

      <Typography
        variant="body2"
        noWrap
        title={title}
        fontWeight={500}
        sx={{ fontSize: { xs: "0.8rem", md: "0.85rem" } }}
      >
        {title}
      </Typography>

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
    </Box>
  );
};
