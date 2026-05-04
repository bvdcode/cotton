import {
  Alert,
  Box,
  Button,
  CircularProgress,
  LinearProgress,
  Paper,
  Stack,
  Tooltip,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import { type MouseEvent, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "react-toastify";
import DeleteSweepIcon from "@mui/icons-material/DeleteSweep";
import HelpOutlineIcon from "@mui/icons-material/HelpOutline";
import {
  adminApi,
  type GcChunkTimelineDto,
  type GcTimelineBucketKind,
} from "../../../shared/api/adminApi";
import {
  settingsApi,
  type StorageSpaceMode,
} from "../../../shared/api/settingsApi";
import {
  getApiErrorMessage,
  showApiErrorToast,
} from "../../../shared/api/httpClient";
import { formatBytes } from "../../../shared/utils/formatBytes";
import { AdminStorageBackendSettings } from "../settings/AdminStorageBackendSettings";
import { storageSpaceOptions } from "../settings/adminGeneralSettingsModel";

type LoadState =
  | { kind: "idle" }
  | { kind: "loading" }
  | { kind: "error"; message: string };

type TriggerState =
  | { kind: "idle" }
  | { kind: "loading" }
  | { kind: "error"; message: string };

type TimelinePoint = {
  bucketStartUtc: string;
  chunkCount: number;
  sizeBytes: number;
};

const MIN_SLOT_COUNT_BY_BUCKET: Record<GcTimelineBucketKind, number> = {
  day: 7,
  hour: 24,
};

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
  }).format(parsed);
};

const formatCount = (value: number): string =>
  new Intl.NumberFormat().format(value);

const parseDateToUtc = (value: string): Date => {
  const withZone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value) ? value : `${value}Z`;
  const parsed = new Date(withZone);

  if (!Number.isNaN(parsed.getTime())) {
    return parsed;
  }

  return new Date(value);
};

const bucketStepMs = (bucket: GcTimelineBucketKind): number =>
  bucket === "day" ? 24 * 60 * 60 * 1000 : 60 * 60 * 1000;

const toBucketIndex = (
  value: string,
  bucket: GcTimelineBucketKind,
): number | null => {
  const parsed = parseDateToUtc(value);
  if (Number.isNaN(parsed.getTime())) {
    return null;
  }

  return Math.floor(parsed.getTime() / bucketStepMs(bucket));
};

const fromBucketIndexToIso = (
  index: number,
  bucket: GcTimelineBucketKind,
): string => new Date(index * bucketStepMs(bucket)).toISOString();

const formatSlotLabel = (
  value: string,
  bucket: GcTimelineBucketKind,
): string => {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  if (bucket === "day") {
    return new Intl.DateTimeFormat(undefined, {
      month: "short",
      day: "2-digit",
      timeZone: "UTC",
    }).format(parsed);
  }

  return new Intl.DateTimeFormat(undefined, {
    hour: "2-digit",
    minute: "2-digit",
  }).format(parsed);
};

export const AdminStorageStatisticsPage = () => {
  const { t } = useTranslation(["admin", "common"]);

  const [timeline, setTimeline] = useState<GcChunkTimelineDto | null>(null);
  const [bucket, setBucket] = useState<GcTimelineBucketKind>("day");
  const [storageSpaceMode, setStorageSpaceMode] =
    useState<StorageSpaceMode>("Optimal");
  const [storageSpaceModeLoading, setStorageSpaceModeLoading] = useState(true);
  const [storageSpaceModeSaving, setStorageSpaceModeSaving] = useState(false);
  const [loadState, setLoadState] = useState<LoadState>({ kind: "loading" });
  const [triggerState, setTriggerState] = useState<TriggerState>({
    kind: "idle",
  });

  const [refreshVersion, setRefreshVersion] = useState(0);

  useEffect(() => {
    let isActive = true;

    const loadTimeline = async () => {
      try {
        const result = await adminApi.getGcChunksTimeline({
          bucket,
        });

        if (!isActive) {
          return;
        }

        setTimeline(result);
        setLoadState({ kind: "idle" });
      } catch (error) {
        if (!isActive) {
          return;
        }

        const message = getApiErrorMessage(error);
        if (message) {
          setLoadState({ kind: "error", message });
          return;
        }

        setLoadState({
          kind: "error",
          message: t("storageStatistics.errors.loadFailed"),
        });
      }
    };

    void loadTimeline();

    return () => {
      isActive = false;
    };
  }, [bucket, refreshVersion, t]);

  useEffect(() => {
    let isActive = true;

    const loadStorageSpaceMode = async () => {
      setStorageSpaceModeLoading(true);

      try {
        const nextStorageSpaceMode = await settingsApi.getStorageSpaceMode();

        if (!isActive) {
          return;
        }

        setStorageSpaceMode(nextStorageSpaceMode);
      } catch {
        if (!isActive) {
          return;
        }

        toast.error(t("settings.errors.loadFailed"), {
          toastId: "admin:storage-statistics:storage-space-mode:load-failed",
        });
      } finally {
        if (isActive) {
          setStorageSpaceModeLoading(false);
        }
      }
    };

    void loadStorageSpaceMode();

    return () => {
      isActive = false;
    };
  }, [t]);

  const handleBucketChange = (
    _: MouseEvent<HTMLElement>,
    nextBucket: GcTimelineBucketKind | null,
  ) => {
    if (!nextBucket || nextBucket === bucket) {
      return;
    }

    setLoadState({ kind: "loading" });
    setBucket(nextBucket);
  };

  const handleStorageSpaceModeChange = (
    _: MouseEvent<HTMLElement>,
    nextMode: StorageSpaceMode | null,
  ) => {
    if (
      !nextMode ||
      nextMode === storageSpaceMode ||
      storageSpaceModeLoading ||
      storageSpaceModeSaving
    ) {
      return;
    }

    const previousMode = storageSpaceMode;
    setStorageSpaceMode(nextMode);
    setStorageSpaceModeSaving(true);

    settingsApi
      .setStorageSpaceMode(nextMode)
      .then(() => {
        toast.success(t("settings.state.saved"), {
          toastId: "admin:storage-statistics:storage-space-mode:saved",
        });
      })
      .catch((error) => {
        setStorageSpaceMode(previousMode);
        showApiErrorToast(
          error,
          t("settings.errors.saveFailed"),
          "admin:storage-statistics:storage-space-mode:save-failed",
        );
      })
      .finally(() => {
        setStorageSpaceModeSaving(false);
      });
  };

  const refreshTimeline = () => {
    setLoadState({ kind: "loading" });
    setRefreshVersion((value) => value + 1);
  };

  const handleTriggerGarbageCollector = async () => {
    setTriggerState({ kind: "loading" });

    try {
      await adminApi.triggerGarbageCollector();
      setTriggerState({ kind: "idle" });
      toast.success(t("storageStatistics.state.triggerGcSuccess"), {
        toastId: "admin:storage-statistics:trigger-gc:success",
      });
      setLoadState({ kind: "loading" });
      setRefreshVersion((value) => value + 1);
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

  const placeholder = t("placeholder", { ns: "common" });
  const isLoading = loadState.kind === "loading";
  const isTriggering = triggerState.kind === "loading";
  const storageSpaceModeHelp = t("settings.general.storageSpaceHelp.description");

  const summaryCards = useMemo(() => {
    if (!timeline) {
      return [];
    }

    const storage = timeline.storage;

    return [
      {
        id: "totalSizeBytes",
        label: t("storageStatistics.summary.totalSizeBytes"),
        value: formatBytes(timeline.totalSizeBytes),
        extra: `${t("storageStatistics.summary.totalChunks")}: ${formatCount(timeline.totalChunks)}`,
      },
      {
        id: "pendingGcStoredSizeBytes",
        label: t("storageStatistics.storage.fields.pendingGcStoredSizeBytes"),
        value: formatBytes(storage.pendingGcStoredSizeBytes),
        extra: `${t("storageStatistics.storage.fields.pendingGcChunkCount")}: ${formatCount(storage.pendingGcChunkCount)}`,
      },
      {
        id: "overdueGcStoredSizeBytes",
        label: t("storageStatistics.storage.fields.overdueGcStoredSizeBytes"),
        value: formatBytes(storage.overdueGcStoredSizeBytes),
        extra: `${t("storageStatistics.storage.fields.overdueGcChunkCount")}: ${formatCount(storage.overdueGcChunkCount)}`,
      },
      {
        id: "dedupSavedBytes",
        label: t("storageStatistics.storage.fields.dedupSavedBytes"),
        value: formatBytes(storage.dedupSavedBytes),
        extra: `${t("storageStatistics.storage.fields.deduplicatedUniqueChunkCount")}: ${formatCount(storage.deduplicatedUniqueChunkCount)}`,
      },
      {
        id: "compressionSavedBytes",
        label: t("storageStatistics.storage.fields.compressionSavedBytes"),
        value: formatBytes(storage.compressionSavedBytes),
        extra: `${t("storageStatistics.storage.fields.storageType")}: ${storage.storageType || placeholder}`,
      },
      {
        id: "totalUniqueChunkStoredSizeBytes",
        label: t(
          "storageStatistics.storage.fields.totalUniqueChunkStoredSizeBytes",
        ),
        value: formatBytes(storage.totalUniqueChunkStoredSizeBytes),
        extra: `${t("storageStatistics.storage.fields.totalUniqueChunkCount")}: ${formatCount(storage.totalUniqueChunkCount)}`,
      },
    ];
  }, [timeline, placeholder, t]);

  const timelinePoints = useMemo<TimelinePoint[]>(() => {
    if (!timeline) {
      return [];
    }

    const currentBucketIndex =
      toBucketIndex(new Date().toISOString(), bucket) ?? 0;
    const pointsByIndex = new Map<number, TimelinePoint>();

    timeline.buckets.forEach((item) => {
      const rawBucketIndex = toBucketIndex(item.bucketStartUtc, bucket);
      if (rawBucketIndex === null) {
        return;
      }
      const bucketIndex = Math.max(rawBucketIndex, currentBucketIndex);

      const existing = pointsByIndex.get(bucketIndex);
      if (existing) {
        pointsByIndex.set(bucketIndex, {
          bucketStartUtc: existing.bucketStartUtc,
          chunkCount: existing.chunkCount + item.chunkCount,
          sizeBytes: existing.sizeBytes + item.sizeBytes,
        });
        return;
      }

      pointsByIndex.set(bucketIndex, {
        bucketStartUtc: fromBucketIndexToIso(bucketIndex, bucket),
        chunkCount: item.chunkCount,
        sizeBytes: item.sizeBytes,
      });
    });

    const sortedEntries = [...pointsByIndex.entries()].sort(
      (a, b) => a[0] - b[0],
    );
    const minSlotCount = MIN_SLOT_COUNT_BY_BUCKET[bucket];

    const rawStartIndex =
      sortedEntries[0]?.[0] ??
      toBucketIndex(timeline.from, bucket) ??
      toBucketIndex(new Date().toISOString(), bucket) ??
      0;
    const startIndex = Math.max(rawStartIndex, currentBucketIndex);
    const endIndex = Math.max(
      sortedEntries[sortedEntries.length - 1]?.[0] ?? startIndex,
      startIndex,
    );
    const slotCount = Math.max(minSlotCount, endIndex - startIndex + 1);

    return Array.from({ length: slotCount }, (_, indexOffset) => {
      const index = startIndex + indexOffset;
      const existing = pointsByIndex.get(index);

      if (existing) {
        return existing;
      }

      return {
        bucketStartUtc: fromBucketIndexToIso(index, bucket),
        chunkCount: 0,
        sizeBytes: 0,
      };
    });
  }, [bucket, timeline]);

  const maxTimelinePointSize = useMemo(() => {
    if (timelinePoints.length === 0) {
      return 1;
    }

    const maxValue = Math.max(...timelinePoints.map((item) => item.sizeBytes));
    return maxValue > 0 ? maxValue : 1;
  }, [timelinePoints]);

  const storageSpaceModeControl = (
    <Stack
      spacing={0.75}
      sx={{ width: "100%" }}
    >
      <Stack direction="row" spacing={0.75} alignItems="center">
        <Typography variant="body2" color="text.secondary">
          {t("settings.general.fields.storageSpaceMode")}
        </Typography>
        <Tooltip title={storageSpaceModeHelp}>
          <HelpOutlineIcon fontSize="small" color="action" />
        </Tooltip>
      </Stack>
      <ToggleButtonGroup
        size="small"
        exclusive
        value={storageSpaceMode}
        onChange={handleStorageSpaceModeChange}
        disabled={storageSpaceModeLoading || storageSpaceModeSaving}
        aria-label={t("settings.general.fields.storageSpaceMode")}
        fullWidth
        sx={{
          width: "100%",
          "& .MuiToggleButton-root": {
            flex: 1,
            minWidth: 0,
            whiteSpace: "normal",
            lineHeight: 1.2,
          },
        }}
      >
        {storageSpaceOptions.map((option) => (
          <ToggleButton key={option} value={option}>
            {t(`settings.general.storageSpaceMode.${option}`)}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>
    </Stack>
  );

  return (
    <Stack spacing={2}>
      <Box sx={{ width: "100%", display: "flex", justifyContent: "center" }}>
      <Paper
        sx={{
          width: "min(100%, 880px)",
          overflow: "hidden",
        }}
      >
        <Stack p={3} spacing={3}>
          <Stack
            direction={{ xs: "column", md: "row" }}
            spacing={1}
            justifyContent="space-between"
            alignItems={{ xs: "stretch", md: "center" }}
          >
            <Stack spacing={0.5}>
              <Typography variant="h6" fontWeight={700}>
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

          {loadState.kind === "error" && (
            <Alert severity="error">{loadState.message}</Alert>
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
                {summaryCards.map((card) => (
                  <Box
                    key={card.id}
                    sx={{
                      p: 1,
                      minWidth: 0,
                    }}
                  >
                    <Stack spacing={0.5}>
                      <Typography
                        variant="caption"
                        color="text.secondary"
                        noWrap
                      >
                        {card.label}
                      </Typography>
                      <Typography variant="h6" fontWeight={700}>
                        {card.value}
                      </Typography>
                      <Typography
                        variant="caption"
                        color="text.secondary"
                        noWrap
                      >
                        {card.extra}
                      </Typography>
                    </Stack>
                  </Box>
                ))}
              </Box>

              <Box sx={{ p: 1 }}>
                <Stack p={2} spacing={2}>
                  <Stack
                    direction={{ xs: "column", md: "row" }}
                    justifyContent="space-between"
                    alignItems={{ xs: "flex-start", md: "center" }}
                    spacing={0.5}
                  >
                    <Typography variant="subtitle1" fontWeight={700}>
                      {t("storageStatistics.timeline.title")}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {formatDateTime(timeline.from)} -{" "}
                      {formatDateTime(timeline.to)}
                    </Typography>
                  </Stack>

                  <Box sx={{ overflowX: "auto", pb: 1 }}>
                    <Box
                      sx={{
                        minWidth: `${timelinePoints.length * 84}px`,
                        position: "relative",
                        pt: 1,
                      }}
                    >
                      <Box
                        sx={{
                          position: "absolute",
                          left: 0,
                          right: 0,
                          top: 50,
                          height: 2,
                          bgcolor: "divider",
                        }}
                      />

                      <Box
                        sx={{
                          display: "grid",
                          gridTemplateColumns: `repeat(${timelinePoints.length}, minmax(0, 1fr))`,
                          gap: 1,
                          position: "relative",
                          zIndex: 1,
                        }}
                      >
                        {timelinePoints.map((point) => {
                          const relativeSize =
                            point.sizeBytes / maxTimelinePointSize;
                          const dotSize =
                            point.sizeBytes > 0
                              ? 10 + Math.round(relativeSize * 16)
                              : 8;

                          return (
                            <Stack
                              key={point.bucketStartUtc}
                              alignItems="center"
                              spacing={0.5}
                              minWidth={0}
                            >
                              <Typography variant="caption" noWrap>
                                {formatBytes(point.sizeBytes)}
                              </Typography>

                              <Box
                                height={28}
                                display="flex"
                                alignItems="center"
                              >
                                <Box
                                  sx={{
                                    width: dotSize,
                                    height: dotSize,
                                    borderRadius: "50%",
                                    bgcolor: "primary.main",
                                    opacity: point.sizeBytes > 0 ? 1 : 0.3,
                                    border: "2px solid",
                                    borderColor: "background.paper",
                                  }}
                                />
                              </Box>

                              <Typography
                                variant="caption"
                                color="text.secondary"
                                noWrap
                              >
                                {formatSlotLabel(point.bucketStartUtc, bucket)}
                              </Typography>
                              <Typography
                                variant="caption"
                                color="text.secondary"
                                noWrap
                              >
                                {formatCount(point.chunkCount)}
                              </Typography>
                            </Stack>
                          );
                        })}
                      </Box>
                    </Box>
                  </Box>

                  {timeline.buckets.length === 0 && (
                    <Alert severity="info">
                      {t("storageStatistics.state.empty")}
                    </Alert>
                  )}
                </Stack>
              </Box>
            </Stack>
          )}
        </Stack>
      </Paper>
      </Box>
    </Stack>
  );
};
