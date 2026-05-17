import { Alert, Box, Stack, Typography } from "@mui/material";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import type {
  GcChunkTimelineDto,
  GcTimelineBucketKind,
} from "@shared/api/adminApi";
import { formatBytes } from "@shared/utils/formatBytes";
import {
  MIN_SLOT_COUNT_BY_BUCKET,
  formatCount,
  formatDateTime,
  formatSlotLabel,
  fromBucketIndexToIso,
  toBucketIndex,
  type TimelinePoint,
} from "../timelineUtils";

interface GcTimelineChartProps {
  timeline: GcChunkTimelineDto;
  bucket: GcTimelineBucketKind;
}

export const GcTimelineChart = ({ timeline, bucket }: GcTimelineChartProps) => {
  const { t } = useTranslation(["admin"]);

  const points = useMemo<TimelinePoint[]>(() => {
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

  const maxPointSize = useMemo(() => {
    if (points.length === 0) {
      return 1;
    }

    const maxValue = Math.max(...points.map((item) => item.sizeBytes));
    return maxValue > 0 ? maxValue : 1;
  }, [points]);

  return (
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
            {formatDateTime(timeline.from)} - {formatDateTime(timeline.to)}
          </Typography>
        </Stack>

        <Box sx={{ overflowX: "auto", pb: 1 }}>
          <Box
            sx={{
              minWidth: `${points.length * 84}px`,
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
                gridTemplateColumns: `repeat(${points.length}, minmax(0, 1fr))`,
                gap: 1,
                position: "relative",
                zIndex: 1,
              }}
            >
              {points.map((point) => {
                const relativeSize = point.sizeBytes / maxPointSize;
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

                    <Box height={28} display="flex" alignItems="center">
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
          <Alert severity="info">
            {t("storageStatistics.state.empty")}
          </Alert>
        )}
      </Stack>
    </Box>
  );
};
