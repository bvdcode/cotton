import { Divider, Paper, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { ComputationModeSetting } from "./ComputationModeSetting";
import { GeoIpLookupSetting } from "./GeoIpLookupSetting";
import { PrivacyTogglesSetting } from "./PrivacyTogglesSetting";
import { PublicBaseUrlSetting } from "./PublicBaseUrlSetting";
import { ServerUsageSetting } from "./ServerUsageSetting";
import { TimezoneSetting } from "./TimezoneSetting";

export const AdminGeneralSettingsPage = () => {
  const { t } = useTranslation("admin");

  return (
    <Stack>
      <Paper
        sx={{
          maxWidth: 880,
          width: "100%",
          mx: "auto",
          overflow: "hidden",
        }}
      >
        <Stack p={3} spacing={3} divider={<Divider flexItem />}>
          <Typography variant="h5" fontWeight={700}>
            {t("settings.general.title")}
          </Typography>

          <PublicBaseUrlSetting />
          <TimezoneSetting />
          <ComputationModeSetting />
          <PrivacyTogglesSetting />
          <ServerUsageSetting />
          <GeoIpLookupSetting />
        </Stack>
      </Paper>
    </Stack>
  );
};
