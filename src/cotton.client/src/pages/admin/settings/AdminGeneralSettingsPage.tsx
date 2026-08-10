import { Divider, IconButton, Stack, Tooltip } from "@mui/material";
import SettingsSuggestIcon from "@mui/icons-material/SettingsSuggest";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { ComputationModeSetting } from "./ComputationModeSetting";
import { PublicBaseUrlSetting } from "./PublicBaseUrlSetting";
import { ServerUsageSetting } from "./ServerUsageSetting";
import { TimezoneSetting } from "./TimezoneSetting";
import { TrustedProxyIpAddressSetting } from "./TrustedProxyIpAddressSetting";
import { AdminPageSurface } from "../components/AdminPageSurface";
import { AdminPageHeader } from "../components/AdminPageHeader";

export const AdminGeneralSettingsPage = () => {
  const { t } = useTranslation("admin");
  const navigate = useNavigate();

  return (
    <Stack>
      <AdminPageSurface>
        <Stack p={3} spacing={3} divider={<Divider flexItem />}>
          <AdminPageHeader
            title={t("settings.general.title")}
            description={t("settings.general.description")}
            action={
              <Tooltip
                title={t("settings.general.openSetupWizard.description")}
              >
                <IconButton
                  aria-label={t("settings.general.openSetupWizard.title")}
                  onClick={() => navigate("/setup?preview=1")}
                >
                  <SettingsSuggestIcon />
                </IconButton>
              </Tooltip>
            }
          />

          <PublicBaseUrlSetting />
          <TrustedProxyIpAddressSetting />
          <TimezoneSetting />
          <ComputationModeSetting />
          <ServerUsageSetting />
        </Stack>
      </AdminPageSurface>
    </Stack>
  );
};
