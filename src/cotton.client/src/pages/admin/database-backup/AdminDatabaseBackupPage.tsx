import {
  Alert,
  Button,
  CircularProgress,
  Divider,
  LinearProgress,
  Paper,
  Skeleton,
  Stack,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  adminApi,
  type LatestDatabaseBackupDto,
} from "../../../shared/api/adminApi";
import { isAxiosError } from "../../../shared/api/httpClient";
import { formatBytes } from "../../../shared/utils/formatBytes";

type LoadState =
  | { kind: "idle" }
  | { kind: "loading" }
  | { kind: "error"; message: string };

type TriggerFeedback =
  | { kind: "idle" }
  | { kind: "loading" }
  | { kind: "success"; message: string }
  | { kind: "error"; message: string };

const formatDateTime = (value: string): string => {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).format(parsed);
};

export const AdminDatabaseBackupPage = () => {
  const { t } = useTranslation(["admin", "common"]);

  const [backup, setBackup] = useState<LatestDatabaseBackupDto | null>(null);
  const [loadState, setLoadState] = useState<LoadState>({ kind: "loading" });
  const [triggerFeedback, setTriggerFeedback] = useState<TriggerFeedback>({ kind: "idle" });

  const fetchLatestBackup = useCallback(async () => {
    setLoadState({ kind: "loading" });

    try {
      const latest = await adminApi.getLatestDatabaseBackup();
      setBackup(latest);
      setLoadState({ kind: "idle" });
    } catch (error) {
      if (isAxiosError(error)) {
        const message = (error.response?.data as { message?: string } | undefined)
          ?.message;
        if (typeof message === "string" && message.length > 0) {
          setLoadState({ kind: "error", message });
          return;
        }
      }

      setLoadState({
        kind: "error",
        message: t("databaseBackup.errors.loadFailed"),
      });
    }
  }, [t]);

  useEffect(() => {
    void fetchLatestBackup();
  }, [fetchLatestBackup]);

  const handleTriggerBackup = useCallback(async () => {
    setTriggerFeedback({ kind: "loading" });

    try {
      await adminApi.triggerDatabaseBackup();
      setTriggerFeedback({
        kind: "success",
        message: t("databaseBackup.state.triggerSuccess"),
      });
      await fetchLatestBackup();
    } catch (error) {
      if (isAxiosError(error)) {
        const message = (error.response?.data as { message?: string } | undefined)
          ?.message;
        if (typeof message === "string" && message.length > 0) {
          setTriggerFeedback({ kind: "error", message });
          return;
        }
      }

      setTriggerFeedback({
        kind: "error",
        message: t("databaseBackup.errors.triggerFailed"),
      });
    }
  }, [fetchLatestBackup, t]);

  const placeholder = t("placeholder", { ns: "common" });
  const isLoading = loadState.kind === "loading";
  const isTriggering = triggerFeedback.kind === "loading";
  const isInitialLoading = isLoading && backup === null;
  const isRefreshing = isLoading && backup !== null;

  const rows = useMemo(() => {
    if (!backup) {
      return [];
    }

    return [
      {
        id: "backupId",
        label: t("databaseBackup.fields.backupId"),
        value: backup.backupId || placeholder,
      },
      {
        id: "createdAtUtc",
        label: t("databaseBackup.fields.createdAtUtc"),
        value: formatDateTime(backup.createdAtUtc),
      },
      {
        id: "pointerUpdatedAtUtc",
        label: t("databaseBackup.fields.pointerUpdatedAtUtc"),
        value: formatDateTime(backup.pointerUpdatedAtUtc),
      },
      {
        id: "dumpSizeBytes",
        label: t("databaseBackup.fields.dumpSizeBytes"),
        value: formatBytes(backup.dumpSizeBytes),
      },
      {
        id: "chunkCount",
        label: t("databaseBackup.fields.chunkCount"),
        value: String(backup.chunkCount),
      },
      {
        id: "dumpContentHash",
        label: t("databaseBackup.fields.dumpContentHash"),
        value: backup.dumpContentHash || placeholder,
      },
      {
        id: "sourceDatabase",
        label: t("databaseBackup.fields.sourceDatabase"),
        value: backup.sourceDatabase || placeholder,
      },
      {
        id: "sourceHost",
        label: t("databaseBackup.fields.sourceHost"),
        value: backup.sourceHost || placeholder,
      },
      {
        id: "sourcePort",
        label: t("databaseBackup.fields.sourcePort"),
        value: String(backup.sourcePort),
      },
    ];
  }, [backup, placeholder, t]);

  return (
    <Stack spacing={2}>
      <Paper>
        <Stack spacing={2} p={2}>
          <Typography variant="h6" fontWeight={700}>
            {t("databaseBackup.title")}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {t("databaseBackup.description")}
          </Typography>

          <Stack direction="row" spacing={1}>
            <Button
              variant="outlined"
              onClick={() => void fetchLatestBackup()}
              disabled={isLoading || isTriggering}
            >
              {t("databaseBackup.actions.refresh")}
            </Button>
            <Button
              variant="contained"
              onClick={() => void handleTriggerBackup()}
              disabled={isTriggering}
            >
              {isTriggering ? (
                <Stack direction="row" spacing={1} alignItems="center">
                  <CircularProgress size={16} color="inherit" />
                  <Typography variant="button">
                    {t("databaseBackup.actions.triggering")}
                  </Typography>
                </Stack>
              ) : (
                t("databaseBackup.actions.trigger")
              )}
            </Button>
          </Stack>
        </Stack>
      </Paper>

      {loadState.kind === "error" && (
        <Alert severity="error">{loadState.message}</Alert>
      )}

      {triggerFeedback.kind === "error" && (
        <Alert severity="error">{triggerFeedback.message}</Alert>
      )}

      {triggerFeedback.kind === "success" && (
        <Alert severity="success">{triggerFeedback.message}</Alert>
      )}

      <Stack minHeight={4}>
        <LinearProgress
          sx={{
            opacity: isRefreshing ? 1 : 0,
            transition: "opacity 120ms ease",
          }}
        />
      </Stack>

      {isInitialLoading && (
        <Paper>
          <Stack divider={<Divider />}>
            {Array.from({ length: 9 }).map((_, index) => (
              <Stack key={index} spacing={0.75} p={2}>
                <Skeleton variant="text" width={140} height={16} />
                <Skeleton
                  variant="text"
                  width={index === 5 ? "80%" : "42%"}
                  height={24}
                />
              </Stack>
            ))}
          </Stack>
        </Paper>
      )}

      {!isLoading && loadState.kind !== "error" && backup === null && (
        <Alert severity="info">{t("databaseBackup.state.empty")}</Alert>
      )}

      {backup !== null && (
        <Paper>
          <Stack divider={<Divider />}>
            {rows.map((row) => (
              <Stack key={row.id} spacing={0.5} p={2}>
                <Typography variant="caption" color="text.secondary">
                  {row.label}
                </Typography>
                <Typography
                  variant="body1"
                  sx={
                    row.id === "dumpContentHash"
                      ? { fontFamily: "monospace", wordBreak: "break-all" }
                      : undefined
                  }
                >
                  {row.value}
                </Typography>
              </Stack>
            ))}
          </Stack>
        </Paper>
      )}
    </Stack>
  );
};
