import { useCallback, useMemo, useState } from "react";
import type { NavigateFunction } from "react-router-dom";
import type { useTranslation } from "react-i18next";
import type { NodeDto } from "@shared/api/layoutsApi";
import { InterfaceLayoutType } from "@shared/api/layoutsApi";
import type { NodeContentDto } from "@shared/api/nodesApi";
import type { FileSelectionState } from "@shared/hooks/useFileSelection";
import type { useFilesLayout } from "@shared/hooks/useFilesLayout";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";
import {
  buildFileOperations,
  buildFolderOperations,
} from "@shared/utils/operationsAdapters";
import { refreshNodeContent } from "@shared/store/nodesActions";
import { FileListViewFactory, PageHeader } from "../components";
import { calculateFolderStats } from "../utils/nodeUtils";
import type { useFilesContentOperations } from "./useFilesContentOperations";
import type { useFilesEncryptionController } from "./useFilesEncryptionController";
import type { FileListPageLogic } from "./useFileListPageLogic";
import type { useFileMoveController } from "./useFileMoveController";
import type { useFilesSelectionActions } from "./useFilesSelectionActions";

interface UseFilesPagePresentationOptions {
  ancestors: NodeDto[];
  breadcrumbs: React.ComponentProps<typeof PageHeader>["breadcrumbs"];
  content: NodeContentDto | undefined;
  contentOperations: ReturnType<typeof useFilesContentOperations>;
  encryption: ReturnType<typeof useFilesEncryptionController>;
  error: string | null;
  fileListLogic: FileListPageLogic;
  fileSelection: FileSelectionState;
  isHugeFolder: boolean;
  layoutType: InterfaceLayoutType;
  loading: boolean;
  move: ReturnType<typeof useFileMoveController>;
  navigate: NavigateFunction;
  nodeId: string | null;
  selectionActions: ReturnType<typeof useFilesSelectionActions>;
  t: ReturnType<typeof useTranslation>["t"];
  tiles: FileSystemTile[];
  tilesSize: ReturnType<typeof useFilesLayout>["tilesSize"];
  viewMode: ReturnType<typeof useFilesLayout>["viewMode"];
  cycleViewMode: ReturnType<typeof useFilesLayout>["cycleViewMode"];
}

export const useFilesPagePresentation = ({
  ancestors,
  breadcrumbs,
  content,
  contentOperations,
  cycleViewMode,
  encryption,
  error,
  fileListLogic,
  fileSelection,
  isHugeFolder,
  layoutType,
  loading,
  move,
  navigate,
  nodeId,
  selectionActions,
  t,
  tiles,
  tilesSize,
  viewMode,
}: UseFilesPagePresentationOptions) => {
  const [versionDialogFile, setVersionDialogFile] = useState<{
    id: string;
    name: string;
  } | null>(null);
  const stats = useMemo(
    () => calculateFolderStats(content?.nodes, content?.files),
    [content?.files, content?.nodes],
  );

  const handleGoUp = useCallback(() => {
    if (ancestors.length === 0) {
      navigate("/files");
      return;
    }

    const parent = ancestors[ancestors.length - 1];
    navigate(`/files/${parent.id}`);
  }, [ancestors, navigate]);

  const handleOpenVersions = useCallback(
    (fileId: string, fileName: string) => {
      setVersionDialogFile({ id: fileId, name: fileName });
    },
    [],
  );
  const handleCloseVersions = useCallback(() => {
    setVersionDialogFile(null);
  }, []);
  const handleVersionsChanged = useCallback(() => {
    if (nodeId) {
      void refreshNodeContent(nodeId);
    }
  }, [nodeId]);

  const folderOperations = buildFolderOperations(
    contentOperations.folderOps,
    encryption.goToFolder,
    selectionActions.handleShareFolder,
    move.handleCutFolder,
    encryption.handleToggleFolderEncryption,
    encryption.getChildFolderEncryptionPolicyState,
    selectionActions.handleDownloadFolder,
  );
  const fileOperations = buildFileOperations(contentOperations.fileOps, {
    onDownload: fileListLogic.interaction.handleDownloadFile,
    onVersions: handleOpenVersions,
    onShare: fileListLogic.interaction.handleShareFile,
    onCut: move.handleCutFile,
    onClick: fileListLogic.interaction.handleFileClick,
    onMediaClick: fileListLogic.interaction.handleMediaClick,
  });
  const isCreatingInThisFolder =
    contentOperations.folderOps.isCreatingFolder &&
    contentOperations.folderOps.newFolderParentId === nodeId;

  const pageHeaderProps = useMemo(
    (): React.ComponentProps<typeof PageHeader> => ({
      loading,
      breadcrumbs,
      stats,
      viewMode,
      canGoUp: ancestors.length > 0,
      onGoUp: handleGoUp,
      onHomeClick: encryption.goHome,
      onViewModeCycle: cycleViewMode,
      showViewModeToggle: !isHugeFolder,
      showUpload: !!nodeId,
      showNewFile: !!nodeId,
      showNewFolder: !!nodeId,
      onUploadClick: contentOperations.fileUpload.handleUploadClick,
      onNewFileClick: contentOperations.handleCreateMarkdownFile,
      onNewFolderClick: contentOperations.handleNewFolderClick,
      isCreatingFile: contentOperations.isCreatingMarkdownFile,
      isCreatingFolder: contentOperations.folderOps.isCreatingFolder,
      selectionMode: fileSelection.selectionMode,
      selectedCount: fileSelection.selectedCount,
      onToggleSelectionMode: fileSelection.toggleSelectionMode,
      onSelectAll: () => fileSelection.selectAll(tiles),
      onDeselectAll: fileSelection.deselectAll,
      customActionItems: selectionActions.customActionItems,
      breadcrumbsDropHandlers: move.breadcrumbsDropHandlers,
      goUpDropHandlers: move.goUpDropHandlers,
    }),
    [
      ancestors.length,
      breadcrumbs,
      contentOperations.fileUpload.handleUploadClick,
      contentOperations.folderOps.isCreatingFolder,
      contentOperations.handleCreateMarkdownFile,
      contentOperations.handleNewFolderClick,
      contentOperations.isCreatingMarkdownFile,
      cycleViewMode,
      encryption.goHome,
      fileSelection,
      handleGoUp,
      isHugeFolder,
      loading,
      move.breadcrumbsDropHandlers,
      move.goUpDropHandlers,
      nodeId,
      selectionActions.customActionItems,
      stats,
      tiles,
      viewMode,
    ],
  );

  const handleToggleItem = useCallback(
    (
      id: string,
      options?: { shiftKey?: boolean; orderedIds?: ReadonlyArray<string> },
    ) => {
      if (!fileSelection.selectionMode) {
        fileSelection.toggleSelectionMode();
      }
      fileSelection.toggleItem(id, options);
    },
    [fileSelection],
  );

  const fileListViewProps = useMemo(
    (): React.ComponentProps<typeof FileListViewFactory> => ({
      layoutType,
      tiles,
      folderOperations,
      fileOperations,
      onNavigateBack: handleGoUp,
      isCreatingFolder: isCreatingInThisFolder,
      tileSize: tilesSize,
      loading:
        layoutType === InterfaceLayoutType.List
          ? !content && !error
          : (!content && !error) || fileListLogic.isContentTransitioning,
      loadingTitle: t("loading.title"),
      loadingCaption: t("loading.caption"),
      emptyStateText:
        !error && layoutType === InterfaceLayoutType.Tiles
          ? t("empty.all")
          : undefined,
      newFolderName: contentOperations.folderOps.newFolderName,
      onNewFolderNameChange: contentOperations.folderOps.setNewFolderName,
      onConfirmNewFolder: contentOperations.folderOps.handleConfirmNewFolder,
      onCancelNewFolder: contentOperations.folderOps.handleCancelNewFolder,
      folderNamePlaceholder: t("actions.folderNamePlaceholder"),
      fileNamePlaceholder: t("rename.fileNamePlaceholder", { ns: "files" }),
      selectionMode: fileSelection.selectionMode,
      selectedIds: fileSelection.selectedIds,
      onToggleItem: handleToggleItem,
      moveSupport: move.moveSupport,
    }),
    [
      content,
      contentOperations.folderOps,
      error,
      fileListLogic.isContentTransitioning,
      fileOperations,
      fileSelection.selectedIds,
      fileSelection.selectionMode,
      folderOperations,
      handleGoUp,
      handleToggleItem,
      isCreatingInThisFolder,
      layoutType,
      move.moveSupport,
      t,
      tiles,
      tilesSize,
    ],
  );

  const refreshCurrentNodeContent = useCallback(() => {
    if (nodeId) {
      void refreshNodeContent(nodeId);
    }
  }, [nodeId]);

  return {
    fileListViewProps,
    handleCloseVersions,
    handleVersionsChanged,
    pageHeaderProps,
    refreshCurrentNodeContent,
    versionDialogFile,
  };
};
