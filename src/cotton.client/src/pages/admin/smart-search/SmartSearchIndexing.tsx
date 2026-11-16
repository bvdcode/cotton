import {
  Alert,
  Button,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { BooleanSwitchSettingControl } from "../settings/BooleanSwitchSetting";
import type { useSmartSearchSetup } from "./useSmartSearchSetup";

export const SmartSearchIndexing = ({
  setup,
}: {
  setup: ReturnType<typeof useSmartSearchSetup>;
}) => {
  const { t } = useTranslation("admin");
  const {
    indexing,
    status,
    progress,
    numberFormat,
    triggerMutation,
    triggerIndexing,
    canTriggerIndexing,
  } = setup;
  return (
    <Stack spacing={2}>
      <BooleanSwitchSettingControl
        title={t("smartSearch.wizard.enableSearch")}
        description={t("smartSearch.wizard.indexingHelp")}
        value={indexing.value}
        commitValue={indexing.commitValue}
        status={indexing.status}
        loadFailed={indexing.loadFailed}
      />
      {indexing.loadFailed && (
        <Alert severity="error">{t("settings.errors.loadFailed")}</Alert>
      )}
      {indexing.savedValue && (
        <Alert severity="success">{t("smartSearch.wizard.enabledHelp")}</Alert>
      )}
      {status && status.fileCount > 0 && (
        <Stack spacing={1}>
          <Typography variant="body2">
            {t("smartSearch.progress", {
              indexed: numberFormat.format(status.embeddedFileCount),
              total: numberFormat.format(status.fileCount),
            })}
          </Typography>
          <LinearProgress
            color="inherit"
            variant="determinate"
            value={progress * 100}
            aria-label={t("smartSearch.indexingProgress")}
          />
          <Typography variant="body2" color="text.secondary">
            {t("smartSearch.wizard.progressHelp")}
          </Typography>
        </Stack>
      )}
      {status?.fileCount === 0 && (
        <Typography variant="body2" color="text.secondary">
          {t("smartSearch.wizard.empty")}
        </Typography>
      )}
      <Button
        variant="outlined"
        color="inherit"
        loading={triggerMutation.isPending}
        disabled={!canTriggerIndexing}
        onClick={triggerIndexing}
        sx={{ alignSelf: "flex-start" }}
      >
        {t("smartSearch.actions.triggerIndexing")}
      </Button>
      {triggerMutation.isSuccess && (
        <Typography role="status" variant="body2">
          {t("smartSearch.indexingRequested")}
        </Typography>
      )}
    </Stack>
  );
};
