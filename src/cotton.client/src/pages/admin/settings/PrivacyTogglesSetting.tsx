import { Box, IconButton, Stack, Tooltip, Typography } from "@mui/material";
import HelpOutlineIcon from "@mui/icons-material/HelpOutline";
import { useConfirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import { settingsApi } from "../../../shared/api/settingsApi";
import { BooleanSwitchSetting } from "./BooleanSwitchSetting";

export const PrivacyTogglesSetting = () => {
  const { t } = useTranslation("admin");
  const confirm = useConfirm();

  const showTelemetryDetails = async () => {
    try {
      await confirm({
        title: t("settings.general.telemetryDetails.title"),
        content: (
          <Stack spacing={1.5}>
            <Typography variant="body2">
              {t("settings.general.telemetryDetails.intro")}
            </Typography>
            <Box component="ul" sx={{ pl: 3, m: 0 }}>
              <li>{t("settings.general.telemetryDetails.items.instanceId")}</li>
              <li>{t("settings.general.telemetryDetails.items.serverUrl")}</li>
              <li>{t("settings.general.telemetryDetails.items.version")}</li>
              <li>{t("settings.general.telemetryDetails.items.users")}</li>
              <li>{t("settings.general.telemetryDetails.items.nodes")}</li>
              <li>{t("settings.general.telemetryDetails.items.files")}</li>
            </Box>
            <Typography variant="body2">
              {t("settings.general.telemetryDetails.outro")}
            </Typography>
          </Stack>
        ),
        hideCancelButton: true,
        confirmationText: t("common:ok"),
      });
    } catch {
      // ignore
    }
  };

  const telemetryHelpButton = (
    <Tooltip title={t("settings.general.telemetryDetails.tooltip")}>
      <IconButton
        size="small"
        onClick={showTelemetryDetails}
        aria-label={t("settings.general.telemetryDetails.tooltip")}
        sx={{ p: 0.25 }}
      >
        <HelpOutlineIcon fontSize="small" />
      </IconButton>
    </Tooltip>
  );

  return (
    <Stack spacing={2.5}>
      <BooleanSwitchSetting
        title={t("settings.general.fields.telemetry")}
        titleAction={telemetryHelpButton}
        description={t("settings.general.help.telemetry")}
        toastIdPrefix="admin-general:telemetry"
        load={settingsApi.getTelemetry}
        save={settingsApi.setTelemetry}
      />
      <BooleanSwitchSetting
        title={t("settings.general.fields.allowDeduplication")}
        description={t("settings.general.help.allowDeduplication")}
        toastIdPrefix="admin-general:allow-deduplication"
        load={settingsApi.getAllowCrossUserDeduplication}
        save={settingsApi.setAllowCrossUserDeduplication}
      />
      <BooleanSwitchSetting
        title={t("settings.general.fields.allowGlobalIndexing")}
        description={t("settings.general.help.allowGlobalIndexing")}
        toastIdPrefix="admin-general:allow-global-indexing"
        load={settingsApi.getAllowGlobalIndexing}
        save={settingsApi.setAllowGlobalIndexing}
      />
    </Stack>
  );
};
