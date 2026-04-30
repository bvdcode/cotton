import {
  Alert,
  Box,
  Button,
  CircularProgress,
  LinearProgress,
  Paper,
  Skeleton,
  Stack,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "react-toastify";
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
  | { kind: "error"; message: string };

const formatDateTime = (value: string): string => {
  const hasExplicitTimeZone = /([zZ]|[+-]\d{2}:\d{2})$/.test(value);
  const normalizedValue = hasExplicitTimeZone ? value : `${value}Z`;
  const parsed = new Date(normalizedValue);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(parsed);
};

export const AdminDatabaseBackupPage = () => {
  const { t } = useTranslation(["admin", "common"]);

  const [backup, setBackup] = useState<LatestDatabaseBackupDto | null>(null);
  const [loadState, setLoadState] = useState<LoadState>({ kind: "loading" });
  const [triggerFeedback, setTriggerFeedback] = useState<TriggerFeedback>({
    kind: "idle",
  });

  const loadLatestBackup = useCallback(async () => {
    try {
      const latest = await adminApi.getLatestDatabaseBackup();
      setBackup(latest);
      setLoadState({ kind: "idle" });
    } catch (error) {
      if (isAxiosError(error)) {
        const message = (
          error.response?.data as { message?: string } | undefined
        )?.message;
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

  const refreshLatestBackup = useCallback(async () => {
    setLoadState({ kind: "loading" });
    await loadLatestBackup();
  }, [loadLatestBackup]);

  useEffect(() => {
    let cancelled = false;

    adminApi
      .getLatestDatabaseBackup()
      .then((latest) => {
        if (cancelled) return;
        setBackup(latest);
        setLoadState({ kind: "idle" });
      })
      .catch((error: unknown) => {
        if (cancelled) return;

        if (isAxiosError(error)) {
          const message = (
            error.response?.data as { message?: string } | undefined
          )?.message;
          if (typeof message === "string" && message.length > 0) {
            setLoadState({ kind: "error", message });
            return;
          }
        }

        setLoadState({
          kind: "error",
          message: t("databaseBackup.errors.loadFailed"),
        });
      });

    return () => {
      cancelled = true;
    };
  }, [t]);

  const handleTriggerBackup = useCallback(async () => {
    setTriggerFeedback({ kind: "loading" });

    try {
      await adminApi.triggerDatabaseBackup();
      setTriggerFeedback({ kind: "idle" });
      toast.success(t("databaseBackup.state.triggerSuccess"), {
        toastId: "admin:database-backup:trigger:success",
      });
      await refreshLatestBackup();
    } catch (error) {
      if (isAxiosError(error)) {
        const message = (
          error.response?.data as { message?: string } | undefined
        )?.message;
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
  }, [refreshLatestBackup, t]);

  const placeholder = t("placeholder", { ns: "common" });
  const isLoading = loadState.kind === "loading";
  const isTriggering = triggerFeedback.kind === "loading";
  const isInitialLoading = isLoading && backup === null;
  const isRefreshing = isLoading && backup !== null;

  const cards = useMemo(() => {
    if (!backup) {
      return [];
    }

    return [
      {
        id: "backupId",
        label: t("databaseBackup.fields.backupId"),
        value: backup.backupId || placeholder,
        mono: true,
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
        mono: true,
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
      <Paper sx={{ overflow: "hidden" }}>
        <Stack p={2} spacing={2}>
          <Stack
            direction={{ xs: "column", md: "row" }}
            spacing={1}
            justifyContent="space-between"
            alignItems={{ xs: "stretch", md: "center" }}
          >
            <Typography variant="h6" fontWeight={700}>
              {t("databaseBackup.title")}
            </Typography>

            <Stack
              direction={{ xs: "column", sm: "row" }}
              spacing={1}
              useFlexGap
              sx={{ flexWrap: "wrap" }}
            >
              <Button
                variant="outlined"
                onClick={() => void refreshLatestBackup()}
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

          {loadState.kind === "error" && (
            <Alert severity="error">{loadState.message}</Alert>
          )}

          {triggerFeedback.kind === "error" && (
            <Alert severity="error">{triggerFeedback.message}</Alert>
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
            <Box
              sx={{
                display: "grid",
                gap: 1,
                gridTemplateColumns: {
                  xs: "repeat(1, minmax(0, 1fr))",
                  sm: "repeat(2, minmax(0, 1fr))",
                  md: "repeat(3, minmax(0, 1fr))",
                },
              }}
            >
              {Array.from({ length: 6 }).map((_, index) => (
                <Box key={index} sx={{ p: 1.5 }}>
                  <Skeleton variant="text" width={140} height={16} />
                  <Skeleton
                    variant="text"
                    width={index % 2 === 0 ? "54%" : "72%"}
                    height={28}
                  />
                  <Skeleton variant="text" width="66%" height={14} />
                </Box>
              ))}
            </Box>
          )}

          {!isLoading && loadState.kind !== "error" && backup === null && (
            <Alert severity="info">{t("databaseBackup.state.empty")}</Alert>
          )}

          {backup !== null && (
            <Box
              sx={{
                display: "grid",
                gap: 1,
                gridTemplateColumns: {
                  xs: "repeat(1, minmax(0, 1fr))",
                  sm: "repeat(2, minmax(0, 1fr))",
                  md: "repeat(3, minmax(0, 1fr))",
                },
              }}
            >
              {cards.map((card) => (
                <Box
                  key={card.id}
                  sx={{ p: 1.5, minWidth: 0 }}
                >
                  <Typography variant="caption" color="text.secondary" noWrap>
                    {card.label}
                  </Typography>
                  <Typography
                    variant="h6"
                    fontWeight={700}
                    sx={
                      card.mono
                        ? { fontFamily: "monospace", wordBreak: "break-all" }
                        : undefined
                    }
                  >
                    {card.value}
                  </Typography>
                </Box>
              ))}
            </Box>
          )}

          <Alert severity="info">
            {t("databaseBackup.state.restoreIfEmptyHint")}
          </Alert>
        </Stack>
      </Paper>
    </Stack>
  );
};
