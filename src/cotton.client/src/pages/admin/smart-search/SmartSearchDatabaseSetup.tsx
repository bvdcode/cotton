import {
  Alert,
  Button,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { ReactNode } from "react";
import { PgvectorDockerSetup } from "./PgvectorDockerSetup";
import { PgvectorNativeSetup } from "./PgvectorNativeSetup";
import { PgvectorActivation } from "./PgvectorActivation";
import { PgvectorIndexSetup } from "./PgvectorIndexSetup";
import type { useSmartSearchSetup } from "./useSmartSearchSetup";

export const SmartSearchDatabaseSetup = ({
  setup,
  nextAction,
}: {
  setup: ReturnType<typeof useSmartSearchSetup>;
  nextAction: ReactNode;
}) => {
  const { t } = useTranslation("admin");
  const {
    status,
    statusQuery,
    environment,
    changeEnvironment,
    refresh,
    enableMutation,
    failure,
    activationError,
    needsInstallation,
  } = setup;
  if (!status) {
    return null;
  }
  const environmentControl = (
    <ToggleButtonGroup
      value={environment}
      exclusive
      size="small"
      aria-label={t("smartSearch.installation.where")}
      onChange={(_, value: string | null) => changeEnvironment(value)}
      sx={{ flexShrink: 0 }}
    >
      <ToggleButton value="docker">
        {t("smartSearch.installation.docker")}
      </ToggleButton>
      <ToggleButton value="native">
        {t("smartSearch.installation.native")}
      </ToggleButton>
    </ToggleButtonGroup>
  );
  const checkInstallation = (
    <Stack
      direction="row"
      spacing={1}
      sx={{ alignItems: "stretch", "& > button": { minWidth: 0 } }}
    >
      <Button
        variant="outlined"
        color="inherit"
        loading={statusQuery.isFetching}
        disabled={enableMutation.isPending}
        onClick={() => void refresh()}
      >
        {t("smartSearch.actions.checkInstallation")}
      </Button>
      {nextAction}
    </Stack>
  );
  if (status.extensionEnabled) {
    return (
      <PgvectorIndexSetup
        status={status}
        pending={enableMutation.isPending}
        disabled={statusQuery.isFetching || statusQuery.isError}
        error={activationError}
        onPrepare={() => enableMutation.mutate()}
        nextAction={nextAction}
      />
    );
  }
  if (needsInstallation) {
    return (
      <Stack spacing={2}>
        <Alert severity="info">{t("smartSearch.wizard.installHelp")}</Alert>
        <Typography variant="body2">
          {t("smartSearch.installation.where")}
        </Typography>
        {environment === "docker" && (
          <PgvectorDockerSetup
            majorVersion={status.postgresMajorVersion}
            environmentControl={environmentControl}
            action={checkInstallation}
          />
        )}
        {environment === "native" && (
          <PgvectorNativeSetup
            majorVersion={status.postgresMajorVersion}
            environmentControl={environmentControl}
            action={checkInstallation}
          />
        )}
      </Stack>
    );
  }
  return (
    <Stack spacing={2}>
      <Typography variant="body2">
        {t("smartSearch.wizard.activateHelp")}
      </Typography>
      <PgvectorActivation
        databaseName={status.databaseName}
        permissionDenied={failure === "pgvector_permission_denied"}
        error={activationError}
        pending={enableMutation.isPending}
        disabled={statusQuery.isFetching || statusQuery.isError}
        onEnable={() => enableMutation.mutate()}
        onRefresh={() => void refresh()}
        nextAction={nextAction}
      />
    </Stack>
  );
};
