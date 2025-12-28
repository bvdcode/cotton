import { Box, Stack, Typography, alpha, Tooltip } from "@mui/material";
import { type ReactNode } from "react";

export function OptionCard({
  label,
  description,
  icon,
  active,
  onClick,
  disabled = false,
  disabledTooltip,
}: {
  label: string;
  description?: string;
  icon?: ReactNode;
  active: boolean;
  onClick: () => void;
  disabled?: boolean;
  disabledTooltip?: string;
}) {
  const cardContent = (
    <Box
      role="button"
      tabIndex={disabled ? -1 : 0}
      onClick={disabled ? undefined : onClick}
      onKeyDown={(e) => {
        if (disabled) return;
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onClick();
        }
      }}
      sx={{
        borderRadius: 2,
        p: { xs: 1.5, sm: 1.75, md: 2 },
        minHeight: { xs: 100, sm: 110, md: 120 },
        border: (theme) =>
          active
            ? `1.5px solid ${theme.palette.primary.main}`
            : `1px solid ${theme.palette.divider}`,
        background: (theme) =>
          disabled
            ? alpha(theme.palette.text.disabled, 0.05)
            : active
            ? theme.palette.mode === "dark"
              ? `linear-gradient(145deg, ${alpha(
                  theme.palette.primary.main,
                  0.2,
                )}, ${alpha(theme.palette.secondary.main, 0.15)})`
              : `linear-gradient(145deg, ${alpha(
                  theme.palette.primary.main,
                  0.1,
                )}, ${alpha(theme.palette.secondary.main, 0.1)})`
            : alpha(theme.palette.text.primary, 0.02),
        boxShadow: (theme) =>
          disabled
            ? "none"
            : active
            ? `0 15px 55px ${alpha(
                theme.palette.primary.main,
                0.05,
              )}, 0 8px 20px ${alpha(theme.palette.primary.main, 0.15)}`
            : `0 6px 18px ${alpha(
                theme.palette.common.black,
                theme.palette.mode === "dark" ? 0.25 : 0.08,
              )}`,
        cursor: disabled ? "not-allowed" : "pointer",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        justifyContent: "space-between",
        gap: { xs: 1, sm: 1.5, md: 2 },
        opacity: disabled ? 0.4 : 1,
        transition: (theme) =>
          theme.transitions.create([
            "border-color",
            "background",
            "box-shadow",
            "transform",
            "opacity",
          ]),
        ":hover": disabled
          ? {}
          : {
              borderColor: "primary.main",
              transform: "translateY(-2px)",
            },
        outline: "none",
      }}
    >
      <Stack spacing={{ xs: 0.4, sm: 0.5, md: 0.6 }} sx={{ flex: 1, minWidth: 0 }}>
        <Typography 
          variant="subtitle1" 
          fontWeight={700}
          sx={{
            fontSize: { xs: "0.875rem", sm: "0.9rem", md: "1rem" },
            lineHeight: 1.3,
          }}
        >
          {label}
        </Typography>
        {description ? (
          <Typography 
            variant="body2" 
            color="text.secondary"
            sx={{
              fontSize: { xs: "0.75rem", sm: "0.8rem", md: "0.875rem" },
              lineHeight: 1.4,
            }}
          >
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
              disabled
                ? theme.palette.text.disabled
                : active
                ? theme.palette.primary.main
                : theme.palette.text.disabled,
            transition: (theme) =>
              theme.transitions.create(["color"], {
                duration: theme.transitions.duration.standard,
                easing: theme.transitions.easing.easeInOut,
              }),
            flexShrink: 0,
            "& > svg": {
              width: { xs: 48, sm: 56, md: 72 },
              height: { xs: 48, sm: 56, md: 72 },
            },
          }}
        >
          {icon}
        </Box>
      )}
    </Box>
  );

  if (disabled && disabledTooltip) {
    return (
      <Tooltip title={disabledTooltip} arrow placement="top">
        {cardContent}
      </Tooltip>
    );
  }

  return cardContent;
}
