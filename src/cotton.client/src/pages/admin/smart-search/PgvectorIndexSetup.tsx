import { Alert, Button, Chip, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { VectorExtensionStatusDto } from "@shared/api/adminApi";
import { formatBytes } from "@shared/utils/formatBytes";
import { SetupCommand } from "./SetupCommand";

interface PgvectorIndexSetupProps {
  status: VectorExtensionStatusDto;
  pending: boolean;
  disabled: boolean;
  error: string | null;
  onPrepare: () => void;
}

const indexErrorKey = (code: string): string => {
  switch (code) {
    case "pgvector_index_permission_denied":
      return "smartSearch.index.permissionDenied";
    case "pgvector_index_incompatible":
      return "smartSearch.index.incompatible";
    case "pgvector_index_invalid_data":
      return "smartSearch.index.invalidData";
    default:
      return "smartSearch.index.failed";
  }
};

export const PgvectorIndexSetup = ({
  status,
  pending,
  disabled,
  error,
  onPrepare,
}: PgvectorIndexSetupProps) => {
  const { t, i18n } = useTranslation("admin");
  const building = status.indexBuilding || pending;
  let indexLabel = t("smartSearch.index.missing");
  if (building) {
    indexLabel = t("smartSearch.index.building");
  } else if (status.indexReady) {
    indexLabel = t("smartSearch.index.ready");
  }

  return (
    <Stack spacing={1.5}>
      <Stack direction="row" flexWrap="wrap" alignItems="center" gap={3}>
        <Stack direction="row" alignItems="center" spacing={1}>
          <Typography>{t("smartSearch.extension")}</Typography>
          <Chip size="small" label={t("smartSearch.enabled")} />
        </Stack>
        <Stack direction="row" alignItems="center" spacing={1}>
          <Typography>{t("smartSearch.index.title")}</Typography>
          <Chip
            size="small"
            color={status.indexReady ? "success" : "default"}
            label={indexLabel}
          />
          {status.indexReady && (
            <Typography variant="body2">
              {formatBytes(status.indexSizeBytes)}
            </Typography>
          )}
        </Stack>
        <Typography>
          {t("smartSearch.vectorCount")}{" "}
          <Typography component="span" fontWeight={600}>
            {new Intl.NumberFormat(i18n.language).format(status.vectorCount)}
          </Typography>
        </Typography>
      </Stack>
      {!building && status.indexErrorCode && (
        <Alert severity="error">
          {t(indexErrorKey(status.indexErrorCode), {
            database: status.databaseName,
          })}
        </Alert>
      )}
      {error && <Alert severity="error">{error}</Alert>}
      {!building &&
        status.indexErrorCode === "pgvector_index_permission_denied" && (
          <SetupCommand command={status.indexCreateSql} />
        )}
      {(!status.indexReady || building) && (
        <Button
          variant="contained"
          loading={building}
          disabled={disabled}
          onClick={onPrepare}
          sx={{ alignSelf: "flex-start" }}
        >
          {t("smartSearch.actions.enable")}
        </Button>
      )}
    </Stack>
  );
};
