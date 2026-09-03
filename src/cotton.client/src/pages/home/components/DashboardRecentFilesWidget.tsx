import { Box, CardActionArea, Skeleton, Typography } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import type { NodeFileManifestDto } from "../../../shared/api/nodesApi";
import { useRecentFilesQuery } from "../../../shared/api/queries/layouts";
import {
  RECENT_FILES_FILTERS,
  type RecentFilesWidgetId,
} from "../dashboardModel";
import { RecentFileItem } from "./RecentFileItem";
import { DashboardQueryError } from "./DashboardQueryError";

const RECENT_FILE_COUNT = 8;
const SKELETON_COUNT = 3;

interface DashboardRecentFilesWidgetProps {
  enabled: boolean;
  layoutId: string | undefined;
  widgetId: RecentFilesWidgetId;
}

export const DashboardRecentFilesWidget = ({
  enabled,
  layoutId,
  widgetId,
}: DashboardRecentFilesWidgetProps) => {
  const { t } = useTranslation(["home", "common"]);
  const navigate = useNavigate();
  const filter = RECENT_FILES_FILTERS[widgetId];
  const query = useRecentFilesQuery(layoutId, RECENT_FILE_COUNT, {
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
    <Box display="grid" gap={0.25}>
      {files.map((file) => (
        <CardActionArea
          key={file.id}
          onClick={() => handleFileClick(file)}
          sx={{ borderRadius: 1, minWidth: 0, width: "100%" }}
        >
          <RecentFileItem file={file} t={t} />
        </CardActionArea>
      ))}
    </Box>
  );
};
