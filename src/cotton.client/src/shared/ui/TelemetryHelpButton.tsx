import { Box, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { HelpButton } from "./HelpButton";

type TelemetryHelpButtonProps = {
  size?: "small" | "medium";
};

export const TelemetryHelpButton = ({
  size = "small",
}: TelemetryHelpButtonProps) => {
  const { t } = useTranslation("common");
  return (
    <HelpButton
      size={size}
      title={t("telemetryDetails.title")}
      tooltip={t("telemetryDetails.tooltip")}
      content={
        <Stack spacing={1.5}>
          <Typography variant="body2">{t("telemetryDetails.intro")}</Typography>
          <Box component="ul" sx={{ pl: 3, m: 0 }}>
            <li>{t("telemetryDetails.items.instanceId")}</li>
            <li>{t("telemetryDetails.items.serverUrl")}</li>
            <li>{t("telemetryDetails.items.version")}</li>
            <li>{t("telemetryDetails.items.users")}</li>
            <li>{t("telemetryDetails.items.nodes")}</li>
            <li>{t("telemetryDetails.items.files")}</li>
          </Box>
          <Typography variant="body2">{t("telemetryDetails.outro")}</Typography>
        </Stack>
      }
    />
  );
};
