import React, { useDeferredValue, useEffect, useMemo } from "react";
import { Alert, Box, IconButton } from "@mui/material";
import { Delete } from "@mui/icons-material";
import {
  FileListViewFactory,
  PageHeader,
  MediaLightbox,
  FilePreviewModal,
  FileConflictDialog,
  DraggingOverlay,
} from "./components";
import { useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useNodesStore } from "../../shared/store/nodesStore";
import { useFolderOperations } from "./hooks/useFolderOperations";
import { useFileUpload } from "./hooks/useFileUpload";
import { useFileOperations } from "./hooks/useFileOperations";
import { useFileInteractionHandlers } from "./hooks/useFileInteractionHandlers";
import { useFilesLayout } from "./hooks/useFilesLayout";
import { useFilesData } from "./hooks/useFilesData";
import { useFilesRealtimeEvents } from "./hooks/useFilesRealtimeEvents";
import { useFileSelection } from "./hooks/useFileSelection";
import { useDeleteSelectedItems } from "./hooks/useDeleteSelectedItems";
import { buildBreadcrumbs, calculateFolderStats } from "./utils/nodeUtils";
import {
  getDropPreparationCaption,
  getDropPreparationTitle,
} from "./utils/dropPreparation";
import { useContentTiles } from "../../shared/hooks/useContentTiles";
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { shareFolder } from "../../shared/utils/shareFolder";
import Loader from "../../shared/ui/Loader";
import { AppToast } from "../../shared/ui/AppToast";
import { useAudioPlayerStore } from "../../shared/store/audioPlayerStore";
import {
  selectGallerySmoothTransitions,
  useLocalPreferencesStore,
} from "../../shared/store/localPreferencesStore";

const HUGE_FOLDER_THRESHOLD = 10_000;

export const FilesPage: React.FC = () => {
  const { t } = useTranslation(["files", "common"]);
  const confirm = useConfirm();
  const navigate = useNavigate();
  const params = useParams<{ nodeId?: string }>();

  const {
    currentNode,
    ancestors,
    contentByNodeId,
    rootNodeId,
    loading,
    error,
    loadRoot,
    loadNode,
    resolveRootInBackground,
    refreshNodeContent,
    deleteFolder,
    optimisticDeleteFile,
  } = useNodesStore();

  const routeNodeId = params.nodeId;

  const { layoutType, setLayoutType, tilesSize, viewMode, cycleViewMode } =
    useFilesLayout();

  // Resolve root node ID on cold start (home route with no persisted root)
  useEffect(() => {
    if (routeNodeId || rootNodeId) return;
    void loadRoot({ force: false, loadChildren: false });
  }, [routeNodeId, rootNodeId, loadRoot]);

  // Always keep root node synced with backend resolver (non-blocking).
  useEffect(() => {
    if (routeNodeId) return;
    resolveRootInBackground({
      loadChildren: layoutType !== InterfaceLayoutType.List,
    });
  }, [routeNodeId, layoutType, resolveRootInBackground]);

  const nodeId = routeNodeId ?? rootNodeId ?? null;
  const content = nodeId ? contentByNodeId[nodeId] : undefined;

  const {
    childrenTotalCount,
    listTotalCount,
    listLoading,
    listError,
    listContent,
    handlePaginationChange,
    handleFolderChanged,
    reloadCurrentNode,
    optimisticUpdateCurrentNodeFilePreviewHash,
  } = useFilesData({
    nodeId,
    layoutType,
    loadNode,
    refreshNodeContent,
  });

  useFilesRealtimeEvents({
    nodeId,
    onInvalidate: reloadCurrentNode,
    onPreviewGenerated: optimisticUpdateCurrentNodeFilePreviewHash,
  });

  const isHugeFolder =
    childrenTotalCount !== null && childrenTotalCount > HUGE_FOLDER_THRESHOLD;

  useEffect(() => {
    if (!isHugeFolder) return;
    if (layoutType === InterfaceLayoutType.List) return;
    setLayoutType(InterfaceLayoutType.List);
  }, [isHugeFolder, layoutType, setLayoutType]);

  useEffect(() => {
    const folderName = currentNode?.name;
    const isRoot = !routeNodeId || ancestors.length === 0;

    if (isRoot) {
      document.title = `Cotton - ${t("title", { ns: "files" })}`;
    } else if (folderName) {
      document.title = `Cotton - ${folderName}`;
    } else {
      document.title = "Cotton";
    }

    return () => {
      document.title = "Cotton";
    };
  }, [currentNode?.name, routeNodeId, ancestors.length, t]);

  const breadcrumbs = useMemo(
    () => buildBreadcrumbs(ancestors, currentNode),
    [ancestors, currentNode],
  );

  const effectiveContent =
    layoutType === InterfaceLayoutType.List ? listContent : content;

  const deferredContent = useDeferredValue(effectiveContent);
  const isContentTransitioning =
    !!effectiveContent && deferredContent !== effectiveContent;

  const { sortedFiles, tiles } = useContentTiles(deferredContent ?? undefined);
  const setScanRootNodeId = useAudioPlayerStore((s) => s.setScanRootNodeId);

  useEffect(() => {
    if (!nodeId) return;
    setScanRootNodeId(nodeId);
  }, [nodeId, setScanRootNodeId]);

  const {
    previewState,
    closePreview,
    handleFileClick,
    handleDownloadFile,
    handleShareFile,
    shareToast,
    setShareToast,
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    getDownloadUrl,
    handleMediaClick,
    setLightboxOpen,
  } = useFileInteractionHandlers({
    sortedFiles,
    audioFallbackNodeId: nodeId ?? undefined,
  });

  const showToast = React.useCallback(
    (message: string) => setShareToast({ open: true, message }),
    [setShareToast],
  );

  const folderOps = useFolderOperations(nodeId, handleFolderChanged);
  const fileUpload = useFileUpload(nodeId, breadcrumbs, content, {
    onToast: showToast,
  });
  const fileOps = useFileOperations(reloadCurrentNode);
  const fileSelection = useFileSelection();

  const smoothGalleryTransitions = useLocalPreferencesStore(
    selectGallerySmoothTransitions,
  );

  const stats = useMemo(
    () => calculateFolderStats(deferredContent?.nodes, deferredContent?.files),
    [deferredContent?.files, deferredContent?.nodes],
  );

  const goToFolder = useMemo(
    () => (folderId: string) => navigate(`/files/${folderId}`),
    [navigate],
  );

  const goHome = useMemo(() => () => navigate("/files"), [navigate]);

  const handleGoUp = React.useCallback(() => {
    if (ancestors.length > 0) {
      const parent = ancestors[ancestors.length - 1];
      navigate(`/files/${parent.id}`);
    } else {
      navigate("/files");
    }
  }, [ancestors, navigate]);

  const handleShareFolder = React.useCallback(
    async (folderId: string, folderName: string) => {
      await shareFolder(folderId, folderName, t, setShareToast);
    },
    [setShareToast, t],
  );

  const onPaginationModelChange = useMemo(
    () => (model: { page: number; pageSize: number }) => {
      handlePaginationChange(model.page, model.pageSize);
    },
    [handlePaginationChange],
  );

  // Build folder operations adapter
  const folderOperations = buildFolderOperations(
    folderOps,
    goToFolder,
    handleShareFolder,
  );

  // Build file operations adapter
  const fileOperations = buildFileOperations(fileOps, {
    onDownload: handleDownloadFile,
    onShare: handleShareFile,
    onClick: handleFileClick,
    onMediaClick: handleMediaClick,
  });

  const handleDeleteSelected = useDeleteSelectedItems({
    nodeId,
    fileSelection,
    tiles,
    confirm,
    t,
    deleteFolder,
    optimisticDeleteFile,
    reloadCurrentNode,
  });

  const isCreatingInThisFolder =
    folderOps.isCreatingFolder && folderOps.newFolderParentId === nodeId;

  const pageHeaderProps = useMemo(
    (): React.ComponentProps<typeof PageHeader> => ({
      loading,
      breadcrumbs,
      stats,
      viewMode,
      canGoUp: ancestors.length > 0,
      onGoUp: handleGoUp,
      onHomeClick: goHome,
      onViewModeCycle: cycleViewMode,
      showViewModeToggle: !isHugeFolder,
      showUpload: !!nodeId,
      showNewFolder: !!nodeId,
      onUploadClick: fileUpload.handleUploadClick,
      onNewFolderClick: folderOps.handleNewFolder,
      isCreatingFolder: folderOps.isCreatingFolder,
      selectionMode: fileSelection.selectionMode,
      selectedCount: fileSelection.selectedCount,
      onSelectAll: () => fileSelection.selectAll(tiles),
      onDeselectAll: fileSelection.deselectAll,
      customActions:
        fileSelection.selectionMode && fileSelection.selectedCount > 0 ? (
          <IconButton
            color="error"
            onClick={() => {
              void handleDeleteSelected();
            }}
            title={t("selection.deleteSelected", { ns: "files" })}
            disabled={loading}
          >
            <Delete />
          </IconButton>
        ) : undefined,
    }),
    [
      ancestors.length,
      breadcrumbs,
      cycleViewMode,
      handleDeleteSelected,
      fileSelection,
      fileUpload.handleUploadClick,
      folderOps.handleNewFolder,
      folderOps.isCreatingFolder,
      goHome,
      handleGoUp,
      isHugeFolder,
      loading,
      nodeId,
      stats,
      t,
      tiles,
      viewMode,
    ],
  );

  const handleToggleItem = React.useCallback(
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
      isCreatingFolder: isCreatingInThisFolder,
      tileSize: tilesSize,
      loading:
        layoutType === InterfaceLayoutType.List
          ? !listContent && !listError
          : (!content && !error) || isContentTransitioning,
      loadingTitle: t("loading.title"),
      loadingCaption: t("loading.caption"),
      emptyStateText:
        layoutType === InterfaceLayoutType.Tiles ? t("empty.all") : undefined,
      newFolderName: folderOps.newFolderName,
      onNewFolderNameChange: folderOps.setNewFolderName,
      onConfirmNewFolder: folderOps.handleConfirmNewFolder,
      onCancelNewFolder: folderOps.handleCancelNewFolder,
      folderNamePlaceholder: t("actions.folderNamePlaceholder"),
      fileNamePlaceholder: t("rename.fileNamePlaceholder", {
        ns: "files",
      }),
      selectionMode: fileSelection.selectionMode,
      selectedIds: fileSelection.selectedIds,
      onToggleItem: handleToggleItem,
      pagination:
        layoutType === InterfaceLayoutType.List
          ? {
              totalCount: listTotalCount,
              loading: listLoading,
              onPaginationModelChange,
            }
          : undefined,
    }),
    [
      content,
      error,
      fileOperations,
      fileSelection.selectionMode,
      fileSelection.selectedIds,
      handleToggleItem,
      folderOperations,
      folderOps.handleCancelNewFolder,
      folderOps.handleConfirmNewFolder,
      folderOps.newFolderName,
      folderOps.setNewFolderName,
      isContentTransitioning,
      isCreatingInThisFolder,
      layoutType,
      listContent,
      listError,
      listLoading,
      listTotalCount,
      onPaginationModelChange,
      t,
      tiles,
      tilesSize,
    ],
  );

  return (
    <>
      {fileUpload.dropPreparation.active && (
        <Loader
          overlay
          title={getDropPreparationTitle(t, fileUpload.dropPreparation)}
          caption={getDropPreparationCaption(t, fileUpload.dropPreparation)}
        />
      )}

      <AppToast
        toast={shareToast}
        onClose={() => setShareToast((prev) => ({ ...prev, open: false }))}
      />

      <DraggingOverlay
        open={fileUpload.isDragging}
        onDragEnter={fileUpload.handleDragEnter}
        onDragOver={fileUpload.handleDragOver}
        onDragLeave={fileUpload.handleDragLeave}
        onDrop={fileUpload.handleDrop}
        label={t("actions.dropFiles")}
      />
      <Box
        width="100%"
        onDragEnter={fileUpload.handleDragEnter}
        onDragOver={fileUpload.handleDragOver}
        onDragLeave={fileUpload.handleDragLeave}
        onDrop={fileUpload.handleDrop}
        sx={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          flex: 1,
          ...(layoutType === InterfaceLayoutType.List && {
            minHeight: 0,
            overflow: "hidden",
          }),
        }}
      >
        <PageHeader
          {...pageHeaderProps}
        />
        {(error || listError) && (
          <Box mb={1} px={1}>
            <Alert severity="error">{error ?? listError}</Alert>
          </Box>
        )}

        <Box
          sx={
            layoutType === InterfaceLayoutType.List
              ? { flex: 1, minHeight: 0, overflow: "hidden", pb: 1 }
              : {}
          }
        >
          <FileListViewFactory {...fileListViewProps} />
        </Box>
      </Box>

      <FilePreviewModal
        isOpen={previewState.isOpen}
        fileId={previewState.fileId}
        fileName={previewState.fileName}
        fileType={previewState.fileType}
        fileSizeBytes={previewState.fileSizeBytes}
        onClose={closePreview}
        onSaved={() => {
          if (nodeId) {
            void refreshNodeContent(nodeId);
          }
        }}
      />

      {lightboxOpen && mediaItems.length > 0 && (
        <MediaLightbox
          items={mediaItems}
          open={lightboxOpen}
          initialIndex={lightboxIndex}
          onClose={() => setLightboxOpen(false)}
          getSignedMediaUrl={getSignedMediaUrl}
          getDownloadUrl={getDownloadUrl}
          smoothTransitions={smoothGalleryTransitions}
        />
      )}

      <FileConflictDialog
        open={fileUpload.conflictDialog.state.open}
        newName={fileUpload.conflictDialog.state.newName}
        onResolve={fileUpload.conflictDialog.onResolve}
        onExited={fileUpload.conflictDialog.onExited}
      />
    </>
  );
};
