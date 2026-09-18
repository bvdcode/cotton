import { Alert, Link } from "@mui/material";
import { useTranslation } from "react-i18next";
import { Link as RouterLink } from "react-router-dom";
import { useComputationStatusQuery } from "@shared/api/queries/admin";
import { ADMIN_GENERAL_SETTINGS_ROUTE } from "@shared/config/adminRoutes";

export const SmartSearchComputationStatus = () => {
  const { t } = useTranslation("admin");
  const service = useComputationStatusQuery();
  const error = service.data?.error;
  if (!service.isError && !error) {
    return null;
  }

  return (
    <Alert severity="warning">
      {service.isError && `${t("settings.general.remoteRunner.statusLoadFailed")} `}
      {t("smartSearch.computation.checkSettings")}{" "}
      <Link
        component={RouterLink}
        to={ADMIN_GENERAL_SETTINGS_ROUTE}
        color="inherit"
      >
        {t("menu.generalSettings")} →{" "}
        {t("settings.general.fields.computionMode")}
      </Link>
    </Alert>
  );
};
