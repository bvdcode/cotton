import { Box, Typography } from "@mui/material";
import type { SxProps, Theme } from "@mui/material/styles";
import type { ReactNode } from "react";

export interface FileSystemItemCardProps {
  icon: ReactNode;
  title: string;
  subtitle?: string;
  onClick?: () => void;
  sx?: SxProps<Theme>;
}

export const FileSystemItemCard = ({
  icon,
  title,
  subtitle,
  onClick,
  sx,
}: FileSystemItemCardProps) => {
  const clickable = typeof onClick === "function";

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
        border: "1px solid",
        borderColor: "divider",
        borderRadius: 2,
        p: {
          xs: 1,
          sm: 1.5,
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
      <Box
        sx={{
          width: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          "& > svg": {
            width: "120%",
            height: "120%",
          },
        }}
      >
        {icon}
      </Box>

      <Typography variant="body2" noWrap title={title} fontWeight={500}>
        {title}
      </Typography>

      {subtitle && (
        <Typography
          variant="caption"
          color="text.secondary"
          display="block"
          noWrap
          title={subtitle}
        >
          {subtitle}
        </Typography>
      )}
    </Box>
  );
};
