import { Box, Skeleton, Typography } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { NodeFileManifestDto } from "../../../shared/api/nodesApi";
import { useRecentFilesQuery } from "../../../shared/api/queries/layouts";
import {
  RECENT_FILES_FILTERS,
  type DashboardWidgetSize,
  type RecentFilesWidgetId,
} from "../dashboardModel";
import { RecentFileCard } from "./RecentFileCard";
import { DashboardQueryError } from "./DashboardQueryError";

const RECENT_FILE_ROWS = 3;
const RECENT_FILE_COLUMNS_PER_SIZE = 2;
const SKELETON_COUNT = 3;

interface DashboardRecentFilesWidgetProps {
  enabled: boolean;
  layoutId: string | undefined;
  size: DashboardWidgetSize;
  widgetId: RecentFilesWidgetId;
}

export const DashboardRecentFilesWidget = ({
  enabled,
  layoutId,
  size,
  widgetId,
}: DashboardRecentFilesWidgetProps) => {
  const { t } = useTranslation(["home", "common"]);
  const navigate = useNavigate();
  const filter = RECENT_FILES_FILTERS[widgetId];
  const recentFileCount =
    size * RECENT_FILE_COLUMNS_PER_SIZE * RECENT_FILE_ROWS;
  const query = useRecentFilesQuery(layoutId, recentFileCount, {
    ...filter,
    excludeClientEncrypted: true,
    enabled,
  });
  const files = query.data ?? [];

  const handleFileClick = (file: NodeFileManifestDto): void => {
    if (!file.nodeId) {
      return;
    }

    navigate(`/files/${file.nodeId}`, {
      state: { selectedFileId: file.id },
    });
  };

  if (query.isPending && files.length === 0) {
    return (
      <Box display="grid" gap={0.75}>
        {Array.from({ length: SKELETON_COUNT }, (_, index) => (
          <Skeleton key={index} variant="rounded" height={52} />
        ))}
      </Box>
    );
  }

  if (query.isError && files.length === 0) {
    return (
      <DashboardQueryError
        message={t("dashboard.recent.loadFailed")}
        onRetry={() => void query.refetch()}
      />
    );
  }

  if (files.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        {t("dashboard.recent.empty")}
      </Typography>
    );
  }

  return (
    <Box
      display="grid"
      gridTemplateColumns="repeat(auto-fit, minmax(min(100%, 240px), 1fr))"
      gap={1}
    >
      {files.map((file) => (
        <RecentFileCard
          key={file.id}
          file={file}
          onClick={() => handleFileClick(file)}
        />
      ))}
    </Box>
  );
};
