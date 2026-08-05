import { Stack } from "@mui/material";
import { useTranslation } from "react-i18next";
import { settingsApi } from "../../../shared/api/settingsApi";
import { TelemetryHelpButton } from "../../../shared/ui/TelemetryHelpButton";
import {
  BooleanSwitchSetting,
  BooleanSwitchSettingControl,
} from "./BooleanSwitchSetting";
import type { UseAutoSavedSettingResult } from "./useAutoSavedSetting";

type PrivacyTogglesSettingProps = {
  telemetrySetting: UseAutoSavedSettingResult<boolean>;
  highlightSettingId?: string | null;
  highlightKey?: string;
};

export const PrivacyTogglesSetting = ({
  telemetrySetting,
  highlightSettingId = null,
  highlightKey,
}: PrivacyTogglesSettingProps) => {
  const { t } = useTranslation("admin");

  return (
    <Stack spacing={2.5}>
      <BooleanSwitchSettingControl
        title={t("settings.general.fields.telemetry")}
        titleAction={<TelemetryHelpButton />}
        description={t("settings.general.help.telemetry")}
        value={telemetrySetting.value}
        commitValue={telemetrySetting.commitValue}
        status={telemetrySetting.status}
        loadFailed={telemetrySetting.loadFailed}
        highlight={highlightSettingId === "telemetry"}
        highlightKey={highlightKey}
      />
      <BooleanSwitchSetting
        title={t("settings.general.fields.allowDeduplication")}
        description={t("settings.general.help.allowDeduplication")}
        toastIdPrefix="admin-general:allow-deduplication"
        load={settingsApi.getAllowCrossUserDeduplication}
        save={settingsApi.setAllowCrossUserDeduplication}
        highlight={highlightSettingId === "allowCrossUserDeduplication"}
        highlightKey={highlightKey}
      />
    </Stack>
  );
};
