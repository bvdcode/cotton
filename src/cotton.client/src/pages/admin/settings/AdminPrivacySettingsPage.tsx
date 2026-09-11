import { Divider, Stack } from "@mui/material";
import { useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { PrivacyTogglesSetting } from "./PrivacyTogglesSetting";
import { GeoIpLookupSetting } from "./GeoIpLookupSetting";
import { AdminPageSurface } from "../components/AdminPageSurface";
import { settingsApi } from "../../../shared/api/settingsApi";
import { useAutoSavedSetting } from "./useAutoSavedSetting";
import { AdminPageHeader } from "../components/AdminPageHeader";
import { readStringProperty } from "../../../shared/utils/typeGuards";

export const AdminPrivacySettingsPage = () => {
  const { t } = useTranslation("admin");
  const location = useLocation();
  const highlightSettingId = readStringProperty(
    location.state,
    "highlightSettingId",
  );
  const telemetrySetting = useAutoSavedSetting<boolean>({
    initial: false,
    load: settingsApi.getTelemetry,
    save: settingsApi.setTelemetry,
    toastIdPrefix: "admin-general:telemetry",
    loadErrorMessage: t("settings.errors.loadFailed"),
    saveErrorMessage: t("settings.errors.saveFailed"),
  });
  const telemetryEnabled =
    !telemetrySetting.loadFailed &&
    telemetrySetting.status !== "loading" &&
    telemetrySetting.savedValue;

  return (
    <Stack>
      <AdminPageSurface>
        <Stack p={3} spacing={3} divider={<Divider flexItem />}>
          <AdminPageHeader
            title={t("settings.privacy.title")}
            description={t("settings.privacy.description")}
          />

          <PrivacyTogglesSetting
            telemetrySetting={telemetrySetting}
            highlightSettingId={highlightSettingId}
            highlightKey={location.key}
          />
          <GeoIpLookupSetting
            telemetryEnabled={telemetryEnabled}
            highlight={highlightSettingId === "geoIpLookupMode"}
            highlightKey={location.key}
          />
        </Stack>
      </AdminPageSurface>
    </Stack>
  );
};
