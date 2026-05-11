import { Stack, Typography } from "@mui/material";
import type { ReactNode } from "react";
import type { SaveStatus } from "./useAutoSavedSetting";
import { AdminSettingStatusIndicator } from "./AdminSettingStatusIndicator";

type SettingsSectionProps = {
  title: ReactNode;
  titleAction?: ReactNode;
  description?: ReactNode;
  status?: SaveStatus;
  action?: ReactNode;
  children?: ReactNode;
};

export const SettingsSection = ({
  title,
  titleAction,
  description,
  status = "idle",
  action,
  children,
}: SettingsSectionProps) => (
  <Stack spacing={1.25}>
    <Stack
      direction="row"
      spacing={1}
      alignItems="flex-start"
      justifyContent="space-between"
    >
      <Stack direction="column" spacing={0.25} minWidth={0} flex={1}>
        <Stack
          direction="row"
          spacing={1}
          alignItems="center"
          minWidth={0}
        >
          <Typography variant="subtitle1" fontWeight={700}>
            {title}
          </Typography>
          {titleAction}
          <AdminSettingStatusIndicator status={status} />
        </Stack>
        {description && (
          <Typography variant="caption" color="text.secondary">
            {description}
          </Typography>
        )}
      </Stack>
      {action}
    </Stack>
    {children}
  </Stack>
);
