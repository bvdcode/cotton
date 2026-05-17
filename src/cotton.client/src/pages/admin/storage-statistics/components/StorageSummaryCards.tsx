import { Box, Stack, Typography } from "@mui/material";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import type { GcChunkTimelineDto } from "@shared/api/adminApi";
import { formatBytes } from "@shared/utils/formatBytes";
import { formatCount } from "../timelineUtils";

interface StorageSummaryCardsProps {
  timeline: GcChunkTimelineDto;
}

export const StorageSummaryCards = ({ timeline }: StorageSummaryCardsProps) => {
  const { t } = useTranslation(["admin", "common"]);
  const placeholder = t("placeholder", { ns: "common" });

  const cards = useMemo(() => {
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

  return (
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
        <Box key={card.id} sx={{ p: 1, minWidth: 0 }}>
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
        </Box>
      ))}
    </Box>
  );
};
