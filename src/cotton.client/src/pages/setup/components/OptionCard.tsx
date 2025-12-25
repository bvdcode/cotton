import { Box, Stack, Typography, alpha } from "@mui/material";
import { type ReactNode } from "react";

export function OptionCard({
  label,
  description,
  icon,
  active,
  onClick,
}: {
  label: string;
  description?: string;
  icon?: ReactNode;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <Box
      role="button"
      tabIndex={0}
      onClick={onClick}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onClick();
        }
      }}
      sx={{
        borderRadius: 2,
        p: 2,
        minHeight: 120,
        border: (theme) =>
          active
            ? `1.5px solid ${theme.palette.primary.main}`
            : `1px solid ${theme.palette.divider}`,
        background: (theme) =>
          active
            ? theme.palette.mode === "dark"
              ? `linear-gradient(145deg, ${alpha(theme.palette.primary.main, 0.2)}, ${alpha(theme.palette.secondary.main, 0.15)})`
              : `linear-gradient(145deg, ${alpha(theme.palette.primary.main, 0.1)}, ${alpha(theme.palette.secondary.main, 0.1)})`
            : alpha(theme.palette.text.primary, 0.02),
        boxShadow: (theme) =>
          active
            ? `0 15px 35px ${alpha(theme.palette.primary.main, 0.35)}, 0 8px 20px ${alpha(theme.palette.primary.main, 0.25)}`
            : `0 6px 18px ${alpha(theme.palette.common.black, theme.palette.mode === "dark" ? 0.25 : 0.08)}`,
        cursor: "pointer",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        justifyContent: "space-between",
        gap: 2,
        transition: "all 0.2s ease",
        ":hover": {
          borderColor: "primary.main",
          transform: "translateY(-2px)",
          boxShadow: (theme) =>
            active
              ? `0 20px 45px ${alpha(theme.palette.primary.main, 0.4)}, 0 10px 25px ${alpha(theme.palette.primary.main, 0.3)}`
              : `0 10px 25px ${alpha(theme.palette.common.black, theme.palette.mode === "dark" ? 0.3 : 0.12)}`,
        },
        outline: "none",
      }}
    >
      <Stack spacing={0.6} sx={{ flex: 1 }}>
        <Typography variant="subtitle1" fontWeight={700}>
          {label}
        </Typography>
        {description ? (
          <Typography variant="body2" color="text.secondary">
            {description}
          </Typography>
        ) : null}
      </Stack>
      {icon && (
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            color: (theme) =>
              active
                ? theme.palette.secondary.main
                : theme.palette.text.disabled,
            transition: "color 0.2s ease",
            flexShrink: 0,
            "& > svg": {
              width: 72,
              height: 72,
            },
          }}
        >
          {icon}
        </Box>
      )}
    </Box>
  );
}
