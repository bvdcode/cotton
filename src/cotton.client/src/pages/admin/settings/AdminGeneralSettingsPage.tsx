import {
  Box,
  Divider,
  IconButton,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import SettingsSuggestIcon from "@mui/icons-material/SettingsSuggest";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { ComputationModeSetting } from "./ComputationModeSetting";
import { PublicBaseUrlSetting } from "./PublicBaseUrlSetting";
import { ServerUsageSetting } from "./ServerUsageSetting";
import { TimezoneSetting } from "./TimezoneSetting";

export const AdminGeneralSettingsPage = () => {
  const { t } = useTranslation("admin");
  const navigate = useNavigate();

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
          <Box
            display="flex"
            flexDirection="row"
            justifyContent="space-between"
            alignItems="center"
          >
            <Typography variant="h5" fontWeight={700}>
              {t("settings.general.title")}
            </Typography>

            <Tooltip title={t("settings.general.openSetupWizard.description")}>
              <IconButton
                aria-label={t("settings.general.openSetupWizard.title")}
                onClick={() => navigate("/setup?preview=1")}
              >
                <SettingsSuggestIcon />
              </IconButton>
            </Tooltip>
          </Box>

          <PublicBaseUrlSetting />
          <TimezoneSetting />
          <ComputationModeSetting />
          <ServerUsageSetting />
        </Stack>
      </Paper>
    </Stack>
  );
};
