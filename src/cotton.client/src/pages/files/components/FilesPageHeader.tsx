import React, { useCallback } from "react";
import type { FileSelectionState } from "@shared/hooks/useFileSelection";
import type { useFilesLayout } from "@shared/hooks/useFilesLayout";
import type {
  FileSystemTile,
  FileListStats,
} from "@shared/types/FileListViewTypes";
import type { useFileMoveController } from "../hooks/useFileMoveController";
import type { useFilesContentOperations } from "../hooks/useFilesContentOperations";
import type { useFilesSelectionActions } from "../hooks/useFilesSelectionActions";
import { PageHeader, type PageHeaderActionItem } from "./PageHeader";
import { Star, StarBorder } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { usePinnedFolders } from "@shared/dashboard/usePinnedFolders";

interface FilesPageHeaderProps {
  pinnedFolders: ReturnType<typeof usePinnedFolders>;
  breadcrumbs: React.ComponentProps<typeof PageHeader>["breadcrumbs"];
  canGoUp: boolean;
  stats: FileListStats;
  contentOperations: ReturnType<typeof useFilesContentOperations>;
  cycleViewMode: ReturnType<typeof useFilesLayout>["cycleViewMode"];
  fileSelection: FileSelectionState;
  isHugeFolder: boolean;
  loading: boolean;
  move: ReturnType<typeof useFileMoveController>;
  nodeId: string | null;
  onGoHome: () => void;
  onGoUp: () => void;
  selectionActions: ReturnType<typeof useFilesSelectionActions>;
  tiles: FileSystemTile[];
  viewMode: ReturnType<typeof useFilesLayout>["viewMode"];
}

export const FilesPageHeader: React.FC<FilesPageHeaderProps> = ({
  pinnedFolders,
  breadcrumbs,
  canGoUp,
  stats,
  contentOperations,
  cycleViewMode,
  fileSelection,
  isHugeFolder,
  loading,
  move,
  nodeId,
  onGoHome,
  onGoUp,
  selectionActions,
  tiles,
  viewMode,
}) => {
  const { t } = useTranslation("home");
  const isPinned = nodeId !== null && pinnedFolders.isPinned(nodeId);
  const actions: PageHeaderActionItem[] = [];
  if (nodeId) {
    actions.unshift({
      key: "pin-current-folder",
      icon: isPinned ? <Star /> : <StarBorder />,
      title: t(
        isPinned
          ? "dashboard.pinnedFolders.unpin"
          : "dashboard.pinnedFolders.pin",
      ),
      onClick: () => pinnedFolders.togglePinned(nodeId),
      disabled: loading,
      active: isPinned,
    });
  }
  const handleSelectAll = useCallback(
    () => fileSelection.selectAll(tiles),
    [fileSelection, tiles],
  );

  return (
    <PageHeader
      loading={loading}
      breadcrumbs={breadcrumbs}
      stats={stats}
      viewMode={viewMode}
      canGoUp={canGoUp}
      onGoUp={onGoUp}
      onHomeClick={onGoHome}
      onViewModeCycle={cycleViewMode}
      showViewModeToggle={!isHugeFolder}
      showUpload={!!nodeId}
      showNewFile={!!nodeId}
      showNewFolder={!!nodeId}
      onUploadClick={contentOperations.fileUpload.handleUploadClick}
      onNewFileClick={contentOperations.handleCreateMarkdownFile}
      onNewFolderClick={contentOperations.handleNewFolderClick}
      isCreatingFile={contentOperations.isCreatingMarkdownFile}
      isCreatingFolder={contentOperations.folderOps.isCreatingFolder}
      selectionMode={fileSelection.selectionMode}
      selectedCount={fileSelection.selectedCount}
      onToggleSelectionMode={fileSelection.toggleSelectionMode}
      onSelectAll={handleSelectAll}
      onDeselectAll={fileSelection.deselectAll}
      primaryActionItems={actions}
      customActionItems={selectionActions.customActionItems}
      breadcrumbsDropHandlers={move.breadcrumbsDropHandlers}
      goUpDropHandlers={move.goUpDropHandlers}
    />
  );
};
