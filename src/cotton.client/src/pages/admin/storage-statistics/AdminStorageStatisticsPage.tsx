import {
  Alert,
  Button,
  Divider,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useMemo, useState } from "react";
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

export const AdminStorageStatisticsPage = () => {
  const { t } = useTranslation(["admin", "common"]);

  const [timeline, setTimeline] = useState<GcChunkTimelineDto | null>(null);
  const [bucket, setBucket] = useState<GcTimelineBucketKind>("day");
  const [loadState, setLoadState] = useState<LoadState>({ kind: "loading" });

  const loadTimeline = useCallback(async () => {
    setLoadState({ kind: "loading" });

    try {
      const result = await adminApi.getGcChunksTimeline({
        bucket,
        timezoneOffsetMinutes: -new Date().getTimezoneOffset(),
      });

      setTimeline(result);
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
        message: t("storageStatistics.errors.loadFailed"),
      });
    }
  }, [bucket, t]);

  useEffect(() => {
    void loadTimeline();
  }, [loadTimeline]);

  const handleBucketChange = (
    _: React.MouseEvent<HTMLElement>,
    nextBucket: GcTimelineBucketKind | null,
  ) => {
    if (!nextBucket || nextBucket === bucket) {
      return;
    }

    setBucket(nextBucket);
  };

  const placeholder = t("placeholder", { ns: "common" });
  const isLoading = loadState.kind === "loading";

  const storageRows = useMemo(() => {
    if (!timeline) {
      return [];
    }

    const storage = timeline.storage;
    return [
      {
        id: "storageType",
        label: t("storageStatistics.storage.fields.storageType"),
        value: storage.storageType || placeholder,
      },
      {
        id: "totalUniqueChunkCount",
        label: t("storageStatistics.storage.fields.totalUniqueChunkCount"),
        value: formatCount(storage.totalUniqueChunkCount),
      },
      {
        id: "totalUniqueChunkPlainSizeBytes",
        label: t("storageStatistics.storage.fields.totalUniqueChunkPlainSizeBytes"),
        value: formatBytes(storage.totalUniqueChunkPlainSizeBytes),
      },
      {
        id: "totalUniqueChunkStoredSizeBytes",
        label: t("storageStatistics.storage.fields.totalUniqueChunkStoredSizeBytes"),
        value: formatBytes(storage.totalUniqueChunkStoredSizeBytes),
      },
      {
        id: "referencedUniqueChunkCount",
        label: t("storageStatistics.storage.fields.referencedUniqueChunkCount"),
        value: formatCount(storage.referencedUniqueChunkCount),
      },
      {
        id: "referencedLogicalChunkCount",
        label: t("storageStatistics.storage.fields.referencedLogicalChunkCount"),
        value: formatCount(storage.referencedLogicalChunkCount),
      },
      {
        id: "deduplicatedUniqueChunkCount",
        label: t("storageStatistics.storage.fields.deduplicatedUniqueChunkCount"),
        value: formatCount(storage.deduplicatedUniqueChunkCount),
      },
      {
        id: "dedupSavedBytes",
        label: t("storageStatistics.storage.fields.dedupSavedBytes"),
        value: formatBytes(storage.dedupSavedBytes),
      },
      {
        id: "compressionSavedBytes",
        label: t("storageStatistics.storage.fields.compressionSavedBytes"),
        value: formatBytes(storage.compressionSavedBytes),
      },
      {
        id: "pendingGcChunkCount",
        label: t("storageStatistics.storage.fields.pendingGcChunkCount"),
        value: formatCount(storage.pendingGcChunkCount),
      },
      {
        id: "pendingGcStoredSizeBytes",
        label: t("storageStatistics.storage.fields.pendingGcStoredSizeBytes"),
        value: formatBytes(storage.pendingGcStoredSizeBytes),
      },
      {
        id: "overdueGcChunkCount",
        label: t("storageStatistics.storage.fields.overdueGcChunkCount"),
        value: formatCount(storage.overdueGcChunkCount),
      },
      {
        id: "overdueGcStoredSizeBytes",
        label: t("storageStatistics.storage.fields.overdueGcStoredSizeBytes"),
        value: formatBytes(storage.overdueGcStoredSizeBytes),
      },
    ];
  }, [timeline, placeholder, t]);

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
                  void loadTimeline();
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
              <Paper variant="outlined">
                <Stack divider={<Divider />}>
                  <Stack p={2} spacing={0.5}>
                    <Typography variant="caption" color="text.secondary">
                      {t("storageStatistics.summary.fromUtc")}
                    </Typography>
                    <Typography>{formatDateTime(timeline.fromUtc)}</Typography>
                  </Stack>
                  <Stack p={2} spacing={0.5}>
                    <Typography variant="caption" color="text.secondary">
                      {t("storageStatistics.summary.toUtc")}
                    </Typography>
                    <Typography>{formatDateTime(timeline.toUtc)}</Typography>
                  </Stack>
                  <Stack p={2} spacing={0.5}>
                    <Typography variant="caption" color="text.secondary">
                      {t("storageStatistics.summary.totalChunks")}
                    </Typography>
                    <Typography>{formatCount(timeline.totalChunks)}</Typography>
                  </Stack>
                  <Stack p={2} spacing={0.5}>
                    <Typography variant="caption" color="text.secondary">
                      {t("storageStatistics.summary.totalSizeBytes")}
                    </Typography>
                    <Typography>{formatBytes(timeline.totalSizeBytes)}</Typography>
                  </Stack>
                  <Stack p={2} spacing={0.5}>
                    <Typography variant="caption" color="text.secondary">
                      {t("storageStatistics.summary.generatedAtUtc")}
                    </Typography>
                    <Typography>{formatDateTime(timeline.generatedAtUtc)}</Typography>
                  </Stack>
                </Stack>
              </Paper>

              <Paper variant="outlined">
                <Stack p={2} spacing={1}>
                  <Typography variant="subtitle1" fontWeight={700}>
                    {t("storageStatistics.storage.title")}
                  </Typography>
                  <Stack divider={<Divider />}>
                    {storageRows.map((row) => (
                      <Stack key={row.id} p={1.5} spacing={0.5}>
                        <Typography variant="caption" color="text.secondary">
                          {row.label}
                        </Typography>
                        <Typography>{row.value}</Typography>
                      </Stack>
                    ))}
                  </Stack>
                </Stack>
              </Paper>

              <Paper variant="outlined">
                <Stack p={2} spacing={1}>
                  <Typography variant="subtitle1" fontWeight={700}>
                    {t("storageStatistics.timeline.title")}
                  </Typography>

                  {timeline.buckets.length === 0 ? (
                    <Alert severity="info">{t("storageStatistics.state.empty")}</Alert>
                  ) : (
                    <TableContainer>
                      <Table size="small">
                        <TableHead>
                          <TableRow>
                            <TableCell>
                              {t("storageStatistics.timeline.columns.bucketStartUtc")}
                            </TableCell>
                            <TableCell align="right">
                              {t("storageStatistics.timeline.columns.chunkCount")}
                            </TableCell>
                            <TableCell align="right">
                              {t("storageStatistics.timeline.columns.sizeBytes")}
                            </TableCell>
                          </TableRow>
                        </TableHead>
                        <TableBody>
                          {timeline.buckets.map((item) => (
                            <TableRow key={item.bucketStartUtc}>
                              <TableCell>{formatDateTime(item.bucketStartUtc)}</TableCell>
                              <TableCell align="right">
                                {formatCount(item.chunkCount)}
                              </TableCell>
                              <TableCell align="right">
                                {formatBytes(item.sizeBytes)}
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </TableContainer>
                  )}
                </Stack>
              </Paper>
            </Stack>
          )}
        </Stack>
      </Paper>
    </Stack>
  );
};