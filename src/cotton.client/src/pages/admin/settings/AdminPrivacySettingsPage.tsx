import { Box, Divider, Paper, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { PrivacyTogglesSetting } from "./PrivacyTogglesSetting";
import { GeoIpLookupSetting } from "./GeoIpLookupSetting";

export const AdminPrivacySettingsPage = () => {
  const { t } = useTranslation("admin");

  return (
    <Stack>
      <Box sx={{ width: "100%", display: "flex", justifyContent: "center" }}>
        <Paper
          sx={{
            width: "min(100%, 880px)",
            overflow: "hidden",
          }}
        >
          <Stack p={3} spacing={3} divider={<Divider flexItem />}>
            <Stack spacing={0.5}>
              <Typography variant="h5" fontWeight={700}>
                {t("settings.privacy.title")}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {t("settings.privacy.description")}
              </Typography>
            </Stack>

            <PrivacyTogglesSetting />
            <GeoIpLookupSetting />
          </Stack>
        </Paper>
      </Box>
    </Stack>
  );
};
