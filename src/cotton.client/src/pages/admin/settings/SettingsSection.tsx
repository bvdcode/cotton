import { Box, CircularProgress, Stack, Typography } from "@mui/material";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import type { ReactNode } from "react";
import type { SaveStatus } from "./useAutoSavedSetting";

type SettingsSectionProps = {
  title: ReactNode;
  description?: ReactNode;
  status?: SaveStatus;
  action?: ReactNode;
  children?: ReactNode;
};

const StatusIndicator = ({ status }: { status: SaveStatus }) => {
  if (status === "loading" || status === "saving") {
    return <CircularProgress size={14} thickness={5} />;
  }
  if (status === "saved") {
    return (
      <CheckCircleOutlineIcon
        sx={{ fontSize: 16, color: "success.main" }}
      />
    );
  }
  if (status === "error") {
    return (
      <ErrorOutlineIcon sx={{ fontSize: 16, color: "error.main" }} />
    );
  }
  return null;
};

export const SettingsSection = ({
  title,
  description,
  status = "idle",
  action,
  children,
}: SettingsSectionProps) => (
  <Stack spacing={1.25}>
    <Stack
      direction="row"
      spacing={1}
      alignItems="center"
      justifyContent="space-between"
    >
      <Stack
        direction="row"
        spacing={1}
        alignItems="center"
        minWidth={0}
        flex={1}
      >
        <Typography variant="subtitle1" fontWeight={700}>
          {title}
        </Typography>
        <Box
          sx={{
            width: 16,
            height: 16,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
          }}
        >
          <StatusIndicator status={status} />
        </Box>
      </Stack>
      {action}
    </Stack>
    {description && (
      <Typography variant="body2" color="text.secondary">
        {description}
      </Typography>
    )}
    {children}
  </Stack>
);
