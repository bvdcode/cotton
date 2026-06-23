import {
  Alert,
  Box,
  Button,
  CircularProgress,
  LinearProgress,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import { type MouseEvent, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "@shared/ui/notifications";
import DeleteSweepIcon from "@mui/icons-material/DeleteSweep";
import { type GcTimelineBucketKind } from "@shared/api/adminApi";
import {
  useGcChunksTimelineQuery,
  useTriggerGarbageCollectorMutation,
} from "@shared/api/queries/admin";
import { settingsApi, type StorageSpaceMode } from "@shared/api/settingsApi";
import { getApiErrorMessage, showApiErrorToast } from "@shared/api/httpClient";
import { AdminStorageBackendSettings } from "../settings/AdminStorageBackendSettings";
import { AdminPageSurface } from "../components/AdminPageSurface";
import type { SaveStatus } from "../settings/useAutoSavedSetting";
import { SAVED_STATUS_VISIBLE_MS } from "../settings/adminSettingSaveStatus";
import { GcTimelineChart } from "./components/GcTimelineChart";
import { StorageSpaceModeControl } from "./components/StorageSpaceModeControl";
import { StorageSummaryCards } from "./components/StorageSummaryCards";

type TriggerState =
  | { kind: "idle" }
  | { kind: "loading" }
  | { kind: "error"; message: string };

export const AdminStorageStatisticsPage = () => {
  const { t } = useTranslation(["admin", "common"]);

  const [bucket, setBucket] = useState<GcTimelineBucketKind>("day");
  const [storageSpaceMode, setStorageSpaceMode] =
    useState<StorageSpaceMode>("Optimal");
  const [storageSpaceModeStatus, setStorageSpaceModeStatus] =
    useState<SaveStatus>("loading");
  const [triggerState, setTriggerState] = useState<TriggerState>({
    kind: "idle",
  });

  const storageSpaceModeFlashTimerRef = useRef<number | null>(null);

  const timelineQuery = useGcChunksTimelineQuery({ bucket });
  const timeline = timelineQuery.data ?? null;
  const triggerGcMutation = useTriggerGarbageCollectorMutation();

  useEffect(() => {
    let isActive = true;

    const loadStorageSpaceMode = async () => {
      setStorageSpaceModeStatus("loading");

      try {
        const nextStorageSpaceMode = await settingsApi.getStorageSpaceMode();

        if (!isActive) {
          return;
        }

        setStorageSpaceMode(nextStorageSpaceMode);
        setStorageSpaceModeStatus("idle");
      } catch {
        if (!isActive) {
          return;
        }

        setStorageSpaceModeStatus("error");
        toast.error(t("settings.errors.loadFailed"), {
          toastId: "admin:storage-statistics:storage-space-mode:load-failed",
        });
      }
    };

    void loadStorageSpaceMode();

    return () => {
      isActive = false;
    };
  }, [t]);

  useEffect(
    () => () => {
      if (storageSpaceModeFlashTimerRef.current !== null) {
        window.clearTimeout(storageSpaceModeFlashTimerRef.current);
      }
    },
    [],
  );

  const handleBucketChange = (
    _: MouseEvent<HTMLElement>,
    nextBucket: GcTimelineBucketKind | null,
  ) => {
    if (!nextBucket || nextBucket === bucket) {
      return;
    }

    setBucket(nextBucket);
  };

  const handleStorageSpaceModeChange = (
    _: MouseEvent<HTMLElement>,
    nextMode: StorageSpaceMode | null,
  ) => {
    if (
      !nextMode ||
      nextMode === storageSpaceMode ||
      storageSpaceModeStatus === "loading" ||
      storageSpaceModeStatus === "saving"
    ) {
      return;
    }

    if (storageSpaceModeFlashTimerRef.current !== null) {
      window.clearTimeout(storageSpaceModeFlashTimerRef.current);
      storageSpaceModeFlashTimerRef.current = null;
    }

    const previousMode = storageSpaceMode;
    setStorageSpaceMode(nextMode);
    setStorageSpaceModeStatus("saving");

    settingsApi
      .setStorageSpaceMode(nextMode)
      .then(() => {
        setStorageSpaceModeStatus("saved");
        storageSpaceModeFlashTimerRef.current = window.setTimeout(() => {
          setStorageSpaceModeStatus((current) =>
            current === "saved" ? "idle" : current,
          );
          storageSpaceModeFlashTimerRef.current = null;
        }, SAVED_STATUS_VISIBLE_MS);
      })
      .catch((error) => {
        setStorageSpaceMode(previousMode);
        setStorageSpaceModeStatus("error");
        showApiErrorToast(
          error,
          t("settings.errors.saveFailed"),
          "admin:storage-statistics:storage-space-mode:save-failed",
        );
      });
  };

  const refreshTimeline = () => {
    void timelineQuery.refetch();
  };

  const handleTriggerGarbageCollector = async () => {
    setTriggerState({ kind: "loading" });

    try {
      await triggerGcMutation.mutateAsync();
      setTriggerState({ kind: "idle" });
      toast.success(t("storageStatistics.state.triggerGcSuccess"), {
        toastId: "admin:storage-statistics:trigger-gc:success",
      });
    } catch (error) {
      const message = getApiErrorMessage(error);
      if (message) {
        setTriggerState({ kind: "error", message });
        return;
      }

      setTriggerState({
        kind: "error",
        message: t("storageStatistics.errors.triggerGcFailed"),
      });
    }
  };

  const isLoading = timelineQuery.isPending || timelineQuery.isFetching;
  const loadErrorMessage = timelineQuery.isError
    ? (getApiErrorMessage(timelineQuery.error) ??
      t("storageStatistics.errors.loadFailed"))
    : null;
  const isTriggering = triggerState.kind === "loading";
  const storageSpaceModeDisabled =
    storageSpaceModeStatus === "loading" || storageSpaceModeStatus === "saving";

  const storageSpaceModeControl = (
    <StorageSpaceModeControl
      value={storageSpaceMode}
      status={storageSpaceModeStatus}
      disabled={storageSpaceModeDisabled}
      onChange={handleStorageSpaceModeChange}
    />
  );

  return (
    <Stack spacing={2}>
      <AdminPageSurface>
        <Stack p={3} spacing={3}>
          <Stack
            direction={{ xs: "column", md: "row" }}
            spacing={1}
            justifyContent="space-between"
            alignItems={{ xs: "stretch", md: "center" }}
          >
            <Stack spacing={0.5}>
              <Typography variant="h5" fontWeight={700}>
                {t("storageStatistics.title")}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {t("storageStatistics.description")}
              </Typography>
            </Stack>

            <Stack spacing={1} alignItems={{ xs: "stretch", md: "flex-end" }}>
              <Stack
                direction="row"
                spacing={1}
                useFlexGap
                sx={{ flexWrap: "wrap", justifyContent: { md: "flex-end" } }}
              >
                <ToggleButtonGroup
                  size="small"
                  exclusive
                  value={bucket}
                  onChange={handleBucketChange}
                  disabled={isLoading || isTriggering}
                >
                  <ToggleButton value="hour">
                    {t("storageStatistics.bucket.hour")}
                  </ToggleButton>
                  <ToggleButton value="day">
                    {t("storageStatistics.bucket.day")}
                  </ToggleButton>
                </ToggleButtonGroup>

                <Button
                  variant="contained"
                  onClick={() => void handleTriggerGarbageCollector()}
                  disabled={isLoading || isTriggering}
                  startIcon={
                    isTriggering ? (
                      <CircularProgress size={16} color="inherit" />
                    ) : (
                      <DeleteSweepIcon />
                    )
                  }
                >
                  {isTriggering
                    ? t("storageStatistics.actions.triggeringGc")
                    : t("storageStatistics.actions.triggerGc")}
                </Button>

                <Button
                  variant="outlined"
                  onClick={refreshTimeline}
                  disabled={isLoading || isTriggering}
                >
                  {t("storageStatistics.actions.refresh")}
                </Button>
              </Stack>
            </Stack>
          </Stack>

          <Box flex={1} minWidth={0}>
            <AdminStorageBackendSettings
              showHeader={false}
              onSaved={refreshTimeline}
              storageTypeRightSlot={storageSpaceModeControl}
            />
          </Box>

          {loadErrorMessage && (
            <Alert severity="error">{loadErrorMessage}</Alert>
          )}

          {triggerState.kind === "error" && (
            <Alert severity="error">{triggerState.message}</Alert>
          )}

          <Box minHeight={4}>
            <LinearProgress
              sx={{
                opacity: isLoading ? 1 : 0,
                transition: "opacity 120ms ease",
              }}
            />
          </Box>

          {timeline !== null && (
            <Stack spacing={2}>
              <StorageSummaryCards timeline={timeline} />
              <GcTimelineChart timeline={timeline} bucket={bucket} />
            </Stack>
          )}
        </Stack>
      </AdminPageSurface>
    </Stack>
  );
};
