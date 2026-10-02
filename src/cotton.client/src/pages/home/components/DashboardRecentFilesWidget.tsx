import { Box, Skeleton, Typography } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useFileInteractionHandlers } from "@shared/hooks/useFileInteractionHandlers";
import { FilePreviewModal, MediaLightbox } from "@shared/ui/preview";
import {
  selectGallerySmoothTransitions,
  useUserPreferencesStore,
} from "../../../shared/store/userPreferencesStore";
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
  const interaction = useFileInteractionHandlers({ sortedFiles: files });
  const smoothTransitions = useUserPreferencesStore(
    selectGallerySmoothTransitions,
  );

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
    <>
      <Box
        display="grid"
        gridTemplateColumns="repeat(auto-fit, minmax(min(100%, 240px), 1fr))"
        gap={1}
      >
        {files.map((file) => (
          <RecentFileCard
            key={file.id}
            file={file}
            onClick={() =>
              interaction.handleFileClick(file.id, file.name, file.sizeBytes)
            }
            onOpenLocation={() => navigate(`/files/${file.nodeId}`)}
          />
        ))}
      </Box>
      <FilePreviewModal
        isOpen={interaction.previewState.isOpen}
        fileId={interaction.previewState.fileId}
        fileName={interaction.previewState.fileName}
        fileType={interaction.previewState.fileType}
        fileSizeBytes={interaction.previewState.fileSizeBytes}
        file={interaction.previewState.file}
        onClose={interaction.closePreview}
        onDownload={interaction.handleDownloadFile}
      />
      {interaction.lightboxOpen && interaction.mediaItems.length > 0 && (
        <MediaLightbox
          open={interaction.lightboxOpen}
          initialIndex={interaction.lightboxIndex}
          items={interaction.mediaItems}
          getSignedMediaUrl={interaction.getSignedMediaUrl}
          getDownloadUrl={interaction.getDownloadUrl}
          onShare={interaction.handleShareFile}
          onClose={() => interaction.setLightboxOpen(false)}
          smoothTransitions={smoothTransitions}
        />
      )}
    </>
  );
};
