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
