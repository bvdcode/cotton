import RefreshIcon from "@mui/icons-material/Refresh";
import {
  Alert,
  Button,
  Chip,
  IconButton,
  Skeleton,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { getApiErrorMessage } from "@shared/api/httpClient";
import {
  useEnableVectorExtensionMutation,
  useVectorExtensionStatusQuery,
} from "@shared/api/queries/admin";
import { AdminPageSurface } from "../components/AdminPageSurface";
import { PgvectorDockerSetup } from "./PgvectorDockerSetup";
import { PgvectorNativeSetup } from "./PgvectorNativeSetup";
import { PgvectorActivation } from "./PgvectorActivation";
import { getVectorSetupFailure } from "./smartSearchSetup";

export const AdminSmartSearchPage = () => {
  const { t, i18n } = useTranslation("admin");
  const statusQuery = useVectorExtensionStatusQuery();
  const enableMutation = useEnableVectorExtensionMutation();
  const [environment, setEnvironment] = useState<"docker" | "native">("docker");
  const status = statusQuery.data;
  const failure = getVectorSetupFailure(enableMutation.error);
  const needsInstallation =
    status &&
    (!status.extensionAvailable || failure === "pgvector_package_missing");
  const busy = statusQuery.isFetching || enableMutation.isPending;
  const refresh = async () => {
    const result = await statusQuery.refetch();
    if (
      result.isSuccess &&
      (result.data.extensionEnabled || failure !== "pgvector_permission_denied")
    ) {
      enableMutation.reset();
    }
  };
  const environmentControl = (
    <ToggleButtonGroup
      value={environment}
      exclusive
      size="small"
      aria-label={t("smartSearch.installation.where")}
      onChange={(_, value: string | null) => {
        if (value === "docker" || value === "native") {
          setEnvironment(value);
        }
      }}
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
    <Button
      variant="outlined"
      color="inherit"
      loading={statusQuery.isFetching}
      disabled={enableMutation.isPending}
      onClick={() => void refresh()}
    >
      {t("smartSearch.actions.checkInstallation")}
    </Button>
  );

  return (
    <AdminPageSurface>
      <Stack
        p={{ xs: 2, sm: 3 }}
        spacing={2}
        sx={{
          "& .MuiInputLabel-root.Mui-focused:not(.Mui-error)": {
            color: "text.primary",
          },
        }}
      >
        <Stack
          direction="row"
          justifyContent="space-between"
          alignItems="center"
          spacing={1}
        >
          <Stack
            direction={{ xs: "column", sm: "row" }}
            alignItems={{ sm: "baseline" }}
            gap={{ xs: 0.5, sm: 2 }}
            minWidth={0}
          >
            <Typography component="h1" variant="h5" fontWeight={700}>
              {t("smartSearch.title")}
            </Typography>
            {status && (
              <Typography
                variant="body2"
                color="text.secondary"
                sx={{ overflowWrap: "anywhere" }}
              >
                {t("smartSearch.database", {
                  version: status.postgresMajorVersion,
                  database: status.databaseName,
                })}
              </Typography>
            )}
          </Stack>
          <Tooltip title={t("smartSearch.actions.refresh")}>
            <span>
              <IconButton
                aria-label={t("smartSearch.actions.refresh")}
                disabled={busy}
                onClick={() => void refresh()}
              >
                <RefreshIcon />
              </IconButton>
            </span>
          </Tooltip>
        </Stack>

        {statusQuery.isPending && (
          <Skeleton
            variant="rounded"
            height={120}
            role="status"
            aria-label={t("smartSearch.loading")}
          />
        )}
        {statusQuery.isError && (
          <Alert severity="error">
            {getApiErrorMessage(statusQuery.error) ??
              t("smartSearch.errors.loadFailed")}
          </Alert>
        )}

        {status && (
          <>
            {status.extensionEnabled ? (
              <Stack
                direction="row"
                flexWrap="wrap"
                alignItems="center"
                gap={3}
              >
                <Stack direction="row" alignItems="center" spacing={1}>
                  <Typography>{t("smartSearch.extension")}</Typography>
                  <Chip
                    size="small"
                    color="success"
                    label={t("smartSearch.enabled")}
                  />
                </Stack>
                <Stack direction="row" spacing={1}>
                  <Typography>{t("smartSearch.vectorCount")}</Typography>
                  <Typography fontWeight={600}>
                    {new Intl.NumberFormat(i18n.language).format(
                      status.vectorCount,
                    )}
                  </Typography>
                </Stack>
              </Stack>
            ) : needsInstallation ? (
              <Stack spacing={1.5}>
                <Typography variant="body2">
                  {t("smartSearch.installation.missing")}
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
            ) : (
              <PgvectorActivation
                databaseName={status.databaseName}
                permissionDenied={failure === "pgvector_permission_denied"}
                error={
                  failure === null && enableMutation.isError
                    ? (getApiErrorMessage(enableMutation.error) ??
                      t("smartSearch.errors.enableFailed"))
                    : null
                }
                pending={enableMutation.isPending}
                disabled={statusQuery.isFetching || statusQuery.isError}
                onEnable={() => enableMutation.mutate()}
                onRefresh={() => void refresh()}
              />
            )}
          </>
        )}
      </Stack>
    </AdminPageSurface>
  );
};
