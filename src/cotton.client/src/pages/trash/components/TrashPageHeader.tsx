import React, { useMemo } from "react";
import { Delete, Restore } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { NodeContentDto } from "@shared/api/nodesApi";
import type { FileSelectionState } from "@shared/hooks/useFileSelection";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";
import type { FileBrowserViewMode } from "@shared/utils/viewMode";
import type { useTrashBulkActions, useTrashRestoreActions } from "../hooks";
import { calculateFolderStats } from "../../files/utils/nodeUtils";
import { PageHeader } from "../../files/components";

interface TrashPageHeaderProps {
  actionsLoading: boolean;
  breadcrumbs: React.ComponentProps<typeof PageHeader>["breadcrumbs"];
  bulkActions: ReturnType<typeof useTrashBulkActions>;
  content: NodeContentDto | undefined;
  fileSelection: FileSelectionState;
  loading: boolean;
  onGoHome: () => void;
  onGoUp: () => void;
  onNavigateBreadcrumb: (index: number) => void;
  onViewModeCycle: () => void;
  restore: ReturnType<typeof useTrashRestoreActions>;
  tiles: FileSystemTile[];
  viewMode: FileBrowserViewMode;
}

export const TrashPageHeader: React.FC<TrashPageHeaderProps> = ({
  actionsLoading,
  breadcrumbs,
  bulkActions,
  content,
  fileSelection,
  loading,
  onGoHome,
  onGoUp,
  onNavigateBreadcrumb,
  onViewModeCycle,
  restore,
  tiles,
  viewMode,
}) => {
  const { t } = useTranslation(["trash", "files"]);
  const { deletingTrash, handleDeleteSelected, handleEmptyTrash } = bulkActions;
  const { restoring, restoreSelected } = restore;
  const stats = useMemo(
    () => calculateFolderStats(content?.nodes, content?.files),
    [content?.files, content?.nodes],
  );
  const customActionItems = useMemo(() => {
    const actionsDisabled = actionsLoading || restoring || deletingTrash;

    if (fileSelection.selectionMode && fileSelection.selectedCount > 0) {
      return [
        {
          key: "restore-selected-trash",
          icon: <Restore />,
          title: t("restore.action"),
          onClick: () => {
            void restoreSelected();
          },
          disabled: actionsDisabled,
          color: "primary" as const,
        },
        {
          key: "delete-selected-trash",
          icon: <Delete />,
          title: t("selection.deleteSelected", { ns: "files" }),
          onClick: () => {
            void handleDeleteSelected();
          },
          disabled: actionsDisabled,
          color: "error" as const,
        },
      ];
    }

    if (breadcrumbs.length > 1) {
      return undefined;
    }

    return [
      {
        key: "empty-trash",
        icon: <Delete />,
        title: t("actions.emptyTrash"),
        onClick: handleEmptyTrash,
        disabled:
          actionsLoading || deletingTrash || stats.folders + stats.files === 0,
        color: "error" as const,
      },
    ];
  }, [
    breadcrumbs.length,
    deletingTrash,
    fileSelection.selectedCount,
    fileSelection.selectionMode,
    actionsLoading,
    handleDeleteSelected,
    handleEmptyTrash,
    restoreSelected,
    restoring,
    stats.files,
    stats.folders,
    t,
  ]);

  return (
    <PageHeader
      loading={loading}
      breadcrumbs={breadcrumbs}
      onNavigateBreadcrumb={onNavigateBreadcrumb}
      stats={stats}
      viewMode={viewMode}
      canGoUp={breadcrumbs.length > 1}
      onGoUp={onGoUp}
      onHomeClick={onGoHome}
      onViewModeCycle={onViewModeCycle}
      statsNamespace="trash"
      selectionMode={fileSelection.selectionMode}
      selectedCount={fileSelection.selectedCount}
      onToggleSelectionMode={fileSelection.toggleSelectionMode}
      onSelectAll={() => fileSelection.selectAll(tiles)}
      onDeselectAll={fileSelection.deselectAll}
      customActionItems={customActionItems}
    />
  );
};
