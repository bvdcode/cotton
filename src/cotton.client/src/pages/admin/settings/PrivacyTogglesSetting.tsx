import { Stack } from "@mui/material";
import { useTranslation } from "react-i18next";
import { settingsApi } from "../../../shared/api/settingsApi";
import { BooleanSwitchSetting } from "./BooleanSwitchSetting";

export const PrivacyTogglesSetting = () => {
  const { t } = useTranslation("admin");

  return (
    <Stack spacing={2.5}>
      <BooleanSwitchSetting
        title={t("settings.general.fields.telemetry")}
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
