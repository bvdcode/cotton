import {
  Alert,
  Box,
  Button,
  Paper,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import {
  type MouseEvent,
  useEffect,
  useMemo,
  useState,
} from "react";
import { useTranslation } from "react-i18next";
import {
  adminApi,
  type GcChunkTimelineDto,
  type GcTimelineBucketKind,
} from "../../../shared/api/adminApi";
import { isAxiosError } from "../../../shared/api/httpClient";
import { formatBytes } from "../../../shared/utils/formatBytes";

type LoadState =
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
    second: "2-digit",
  }).format(parsed);
};

const formatCount = (value: number): string =>
  new Intl.NumberFormat().format(value);

const alignUtcToBucketStart = (
  value: Date,
  bucket: GcTimelineBucketKind,
): Date => {
  if (bucket === "day") {
    return new Date(
      Date.UTC(
        value.getUTCFullYear(),
        value.getUTCMonth(),
        value.getUTCDate(),
        0,
        0,
        0,
        0,
      ),
    );
  }

  return new Date(
    Date.UTC(
      value.getUTCFullYear(),
      value.getUTCMonth(),
      value.getUTCDate(),
      value.getUTCHours(),
      0,
      0,
      0,
    ),
  );
};

const addUtcBuckets = (
  value: Date,
  bucket: GcTimelineBucketKind,
  amount: number,
): Date => {
  const next = new Date(value);

  if (bucket === "day") {
    next.setUTCDate(next.getUTCDate() + amount);
    return next;
  }

  next.setUTCHours(next.getUTCHours() + amount);
  return next;
};

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
  const [loadState, setLoadState] = useState<LoadState>({ kind: "loading" });

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
          message: t("storageStatistics.errors.loadFailed"),
        });
      }
    };

    void loadTimeline();

    return () => {
      isActive = false;
    };
  }, [bucket, refreshVersion, t]);

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

  const placeholder = t("placeholder", { ns: "common" });
  const isLoading = loadState.kind === "loading";

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
        label: t("storageStatistics.storage.fields.totalUniqueChunkStoredSizeBytes"),
        value: formatBytes(storage.totalUniqueChunkStoredSizeBytes),
        extra: `${t("storageStatistics.storage.fields.totalUniqueChunkCount")}: ${formatCount(storage.totalUniqueChunkCount)}`,
      },
    ];
  }, [timeline, placeholder, t]);

  const timelinePoints = useMemo<TimelinePoint[]>(() => {
    if (!timeline) {
      return [];
    }

    const sortedBackendPoints = [...timeline.buckets]
      .map((item) => ({
        bucketStartUtc: new Date(item.bucketStartUtc).toISOString(),
        chunkCount: item.chunkCount,
        sizeBytes: item.sizeBytes,
      }))
      .sort((a, b) =>
        new Date(a.bucketStartUtc).getTime() -
        new Date(b.bucketStartUtc).getTime(),
      );

    const pointsByStart = new Map<string, TimelinePoint>();
    sortedBackendPoints.forEach((item) => {
      const normalizedStart = item.bucketStartUtc;
      pointsByStart.set(normalizedStart, {
        bucketStartUtc: normalizedStart,
        chunkCount: item.chunkCount,
        sizeBytes: item.sizeBytes,
      });
    });

    const minSlotCount = MIN_SLOT_COUNT_BY_BUCKET[bucket];

    // If backend returned enough buckets, show them all (minimum acts only as a floor).
    if (sortedBackendPoints.length >= minSlotCount) {
      return sortedBackendPoints;
    }

    const rangeStart = alignUtcToBucketStart(new Date(timeline.from), bucket);

    return Array.from({ length: minSlotCount }, (_, index) => {
      const slotStart = addUtcBuckets(rangeStart, bucket, index).toISOString();
      const existing = pointsByStart.get(slotStart);

      if (existing) {
        return existing;
      }

      return {
        bucketStartUtc: slotStart,
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

  return (
    <Stack spacing={2}>
      <Paper>
        <Stack p={2} spacing={2}>
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

            <Stack direction="row" spacing={1}>
              <ToggleButtonGroup
                size="small"
                exclusive
                value={bucket}
                onChange={handleBucketChange}
                disabled={isLoading}
              >
                <ToggleButton value="hour">
                  {t("storageStatistics.bucket.hour")}
                </ToggleButton>
                <ToggleButton value="day">
                  {t("storageStatistics.bucket.day")}
                </ToggleButton>
              </ToggleButtonGroup>

              <Button
                variant="outlined"
                onClick={() => {
                  setLoadState({ kind: "loading" });
                  setRefreshVersion((value) => value + 1);
                }}
                disabled={isLoading}
              >
                {t("storageStatistics.actions.refresh")}
              </Button>
            </Stack>
          </Stack>

          {loadState.kind === "error" && (
            <Alert severity="error">{loadState.message}</Alert>
          )}

          {isLoading && <Typography>{t("storageStatistics.state.loading")}</Typography>}

          {timeline !== null && (
            <Stack spacing={2}>
              <Box sx={{ display: "flex", gap: 1, overflowX: "auto", pb: 0.5 }}>
                {summaryCards.map((card) => (
                  <Paper
                    key={card.id}
                    variant="outlined"
                    sx={{ p: 1.5, minWidth: 210, flex: "0 0 auto" }}
                  >
                    <Stack spacing={0.5}>
                      <Typography variant="caption" color="text.secondary" noWrap>
                        {card.label}
                      </Typography>
                      <Typography variant="h6" fontWeight={700}>
                        {card.value}
                      </Typography>
                      <Typography variant="caption" color="text.secondary" noWrap>
                        {card.extra}
                      </Typography>
                    </Stack>
                  </Paper>
                ))}
              </Box>

              <Paper variant="outlined">
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
                      {formatDateTime(timeline.from)} - {formatDateTime(timeline.to)}
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
                          const relativeSize = point.sizeBytes / maxTimelinePointSize;
                          const dotSize = point.sizeBytes > 0
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

                              <Box height={28} display="flex" alignItems="center">
                                <Box
                                  sx={{
                                    width: dotSize,
                                    height: dotSize,
                                    borderRadius: "50%",
                                    bgcolor:
                                      point.sizeBytes > 0
                                        ? "primary.main"
                                        : "divider",
                                    border: "2px solid",
                                    borderColor: "background.paper",
                                  }}
                                />
                              </Box>

                              <Typography variant="caption" color="text.secondary" noWrap>
                                {formatSlotLabel(point.bucketStartUtc, bucket)}
                              </Typography>
                              <Typography variant="caption" color="text.secondary" noWrap>
                                {formatCount(point.chunkCount)}
                              </Typography>
                            </Stack>
                          );
                        })}
                      </Box>
                    </Box>
                  </Box>

                  {timeline.buckets.length === 0 && (
                    <Alert severity="info">{t("storageStatistics.state.empty")}</Alert>
                  )}

                  <Typography variant="caption" color="text.secondary">
                    {t("storageStatistics.summary.generatedAtUtc")}: {formatDateTime(timeline.generatedAt)}
                  </Typography>
                </Stack>
              </Paper>
            </Stack>
          )}
        </Stack>
      </Paper>
    </Stack>
  );
};