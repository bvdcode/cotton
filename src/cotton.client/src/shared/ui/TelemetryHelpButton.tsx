import { Box, IconButton, Stack, Tooltip, Typography } from "@mui/material";
import HelpOutlineIcon from "@mui/icons-material/HelpOutline";
import { useConfirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";

type TelemetryHelpButtonProps = {
  size?: "small" | "medium";
};

export const TelemetryHelpButton = ({
  size = "small",
}: TelemetryHelpButtonProps) => {
  const { t } = useTranslation("common");
  const confirm = useConfirm();

  const handleClick = async (e: React.MouseEvent) => {
    e.stopPropagation();
    e.preventDefault();
    try {
      await confirm({
        title: t("telemetryDetails.title"),
        content: (
          <Stack spacing={1.5}>
            <Typography variant="body2">
              {t("telemetryDetails.intro")}
            </Typography>
            <Box component="ul" sx={{ pl: 3, m: 0 }}>
              <li>{t("telemetryDetails.items.instanceId")}</li>
              <li>{t("telemetryDetails.items.serverUrl")}</li>
              <li>{t("telemetryDetails.items.version")}</li>
              <li>{t("telemetryDetails.items.users")}</li>
              <li>{t("telemetryDetails.items.nodes")}</li>
              <li>{t("telemetryDetails.items.files")}</li>
            </Box>
            <Typography variant="body2">
              {t("telemetryDetails.outro")}
            </Typography>
          </Stack>
        ),
        hideCancelButton: true,
        confirmationText: t("ok"),
      });
    } catch {
      // dialog dismissed
    }
  };

  return (
    <Tooltip title={t("telemetryDetails.tooltip")}>
      <IconButton
        size={size}
        onClick={handleClick}
        aria-label={t("telemetryDetails.tooltip")}
        sx={{ p: 0.25 }}
      >
        <HelpOutlineIcon fontSize={size === "small" ? "small" : "medium"} />
      </IconButton>
    </Tooltip>
  );
};
