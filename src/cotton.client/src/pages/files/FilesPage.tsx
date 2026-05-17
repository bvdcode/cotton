import React, { useEffect, useMemo } from "react";
import { Alert, Box } from "@mui/material";
import { ContentCut, ContentPaste, Delete } from "@mui/icons-material";
import { toast } from "react-toastify";
import {
  FileListViewFactory,
  PageHeader,
  MediaLightbox,
  FilePreviewModal,
  FileConflictDialog,
  DraggingOverlay,
} from "./components";
import { useNavigate, useParams, useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useNodesStore } from "../../shared/store/nodesStore";
import { useAuthStore } from "../../shared/store/authStore";
import { useFolderOperations } from "./hooks/useFolderOperations";
import { useFileUpload } from "./hooks/useFileUpload";
import { useFileOperations } from "./hooks/useFileOperations";
import { useFilesLayout } from "./hooks/useFilesLayout";
import { useFilesData } from "./hooks/useFilesData";
import { useFilesRealtimeEvents } from "./hooks/useFilesRealtimeEvents";
import { useFileSelection } from "./hooks/useFileSelection";
import { useDeleteSelectedItems } from "./hooks/useDeleteSelectedItems";
import { buildBreadcrumbs, calculateFolderStats } from "./utils/nodeUtils";
import { getFileTypeInfo } from "@shared/utils/fileTypes";
import {
  getDropPreparationCaption,
  getDropPreparationTitle,
} from "./utils/dropPreparation";
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import { useFolderFileList } from "../../shared/hooks/useFileListSource";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { shareFolder } from "../../shared/utils/shareFolder";
import Loader from "../../shared/ui/Loader";
import { useAudioPlayerStore } from "../../shared/store/audioPlayerStore";
import {
  selectGallerySmoothTransitions,
  useLocalPreferencesStore,
} from "../../shared/store/localPreferencesStore";
import { usePageTitle } from "../../shared/hooks/usePageTitle";
import { useFileMoveController } from "./hooks/useFileMoveController";
import { useFileListPageLogic } from "./hooks/useFileListPageLogic";

const HUGE_FOLDER_THRESHOLD = 100_000;

export const FilesPage: React.FC = () => {
  const { t } = useTranslation(["files", "common"]);
  const confirm = useConfirm();
  const navigate = useNavigate();
  const location = useLocation();
  const params = useParams<{ nodeId?: string }>();
  const pendingSelectedFileIdRef = React.useRef<string | null>(
    (location.state as { selectedFileId?: string } | null)?.selectedFileId ?? null,
  );

  const {
    currentNode,
    ancestors,
    contentByNodeId,
    cacheOwnerUserId,
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
  const currentUserId = useAuthStore((s) => s.user?.id ?? null);

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
  const isUserCacheValid = cacheOwnerUserId === currentUserId;
  const content = nodeId && isUserCacheValid ? contentByNodeId[nodeId] : undefined;

  const {
    childrenTotalCount,
    handleFolderChanged,
    reloadCurrentNode,
    optimisticUpdateCurrentNodeFilePreviewHash,
  } = useFilesData({
    nodeId,
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

  const pageTitle = useMemo(() => {
    const folderName = currentNode?.name;
    const isRoot = !routeNodeId || ancestors.length === 0;

    if (isRoot) {
      return t("title", { ns: "files" });
    }

    return folderName ?? null;
  }, [currentNode?.name, routeNodeId, ancestors.length, t]);

  usePageTitle(pageTitle);

  const breadcrumbs = useMemo(
    () => buildBreadcrumbs(ancestors, currentNode),
    [ancestors, currentNode],
  );

  const effectiveContent = content;

  const fileListSource = useFolderFileList({
    nodeId,
    layoutType,
    deferContent: true,
  });

  const fileListLogic = useFileListPageLogic({
    source: fileListSource,
    sourceKind: "nodes",
  });

  const { isContentTransitioning, sortedFiles, tiles } = fileListLogic;

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
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    getDownloadUrl,
    handleMediaClick,
    setLightboxOpen,
  } = fileListLogic.interaction;

  // Consume selectedFileId from router state (e.g. dashboard → open file)
  React.useEffect(() => {
    const targetId = pendingSelectedFileIdRef.current;
    if (!targetId || sortedFiles.length === 0) return;

    const file = sortedFiles.find((f) => f.id === targetId);
    if (!file) return;

    pendingSelectedFileIdRef.current = null;
    window.history.replaceState({}, "");

    const typeInfo = getFileTypeInfo(file.name, file.contentType ?? null, {
      requiresVideoTranscoding: file.requiresVideoTranscoding ?? false,
    });
    if (typeInfo.type === "image" || typeInfo.type === "video") {
      handleMediaClick(file.id);
    } else {
      handleFileClick(file.id, file.name, file.sizeBytes);
    }
  }, [sortedFiles, handleFileClick, handleMediaClick]);

  const showToast = React.useCallback(
    (message: string, variant: "info" | "error" = "info") => {
      const toastId = `files-upload-${variant}-${message}`;
      if (variant === "error") {
        toast.error(message, { toastId });
        return;
      }

      toast.info(message, { toastId });
    },
    [],
  );

  const folderOps = useFolderOperations(nodeId, handleFolderChanged);
  const fileUpload = useFileUpload(nodeId, breadcrumbs, content, {
    onToast: showToast,
  });
  const fileOps = useFileOperations(reloadCurrentNode);
  const fileSelection = useFileSelection();

  const goUpParentId = ancestors.length > 0
    ? ancestors[ancestors.length - 1].id
    : null;

  const {
    moveSupport,
    clipboardCount,
    handleCutSelection,
    handlePasteHere,
    handleCutFolder,
    handleCutFile,
    goUpDropHandlers,
    breadcrumbsDropHandlers,
  } = useFileMoveController({
    nodeId,
    tiles,
    selectedIds: fileSelection.selectedIds,
    selectedCount: fileSelection.selectedCount,
    goUpParentId,
    showToast,
    t,
  });

  const smoothGalleryTransitions = useLocalPreferencesStore(
    selectGallerySmoothTransitions,
  );

  const stats = useMemo(
    () => calculateFolderStats(effectiveContent?.nodes, effectiveContent?.files),
    [effectiveContent?.files, effectiveContent?.nodes],
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
      await shareFolder(folderId, folderName, t);
    },
    [t],
  );

  // Build folder operations adapter
  const folderOperations = buildFolderOperations(
    folderOps,
    goToFolder,
    handleShareFolder,
    handleCutFolder,
  );

  // Build file operations adapter
  const fileOperations = buildFileOperations(fileOps, {
    onDownload: handleDownloadFile,
    onShare: handleShareFile,
    onCut: handleCutFile,
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

  const customActionItems = useMemo<
    React.ComponentProps<typeof PageHeader>["customActionItems"]
  >(() => {
    const items: NonNullable<
      React.ComponentProps<typeof PageHeader>["customActionItems"]
    > = [];
    if (fileSelection.selectionMode && fileSelection.selectedCount > 0) {
      items.push({
        key: "cut-selected",
        icon: <ContentCut />,
        title: t("move.cut", { ns: "files" }),
        onClick: handleCutSelection,
        disabled: loading,
      });
      items.push({
        key: "delete-selected",
        icon: <Delete />,
        title: t("selection.deleteSelected", { ns: "files" }),
        onClick: () => {
          void handleDeleteSelected();
        },
        disabled: loading,
        color: "error" as const,
      });
    }
    if (clipboardCount > 0 && nodeId) {
      items.push({
        key: "paste-here",
        icon: <ContentPaste />,
        title: t("move.pasteHere", {
          ns: "files",
          count: clipboardCount,
        }),
        onClick: handlePasteHere,
        disabled: loading,
      });
    }
    return items.length > 0 ? items : undefined;
  }, [
    clipboardCount,
    fileSelection.selectionMode,
    fileSelection.selectedCount,
    handleCutSelection,
    handleDeleteSelected,
    handlePasteHere,
    loading,
    nodeId,
    t,
  ]);

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
      customActionItems,
      breadcrumbsDropHandlers,
      goUpDropHandlers,
    }),
    [
      ancestors.length,
      breadcrumbs,
      breadcrumbsDropHandlers,
      customActionItems,
      cycleViewMode,
      goUpDropHandlers,
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
      onNavigateBack: handleGoUp,
      isCreatingFolder: isCreatingInThisFolder,
      tileSize: tilesSize,
      loading:
        layoutType === InterfaceLayoutType.List
          ? !content && !error
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
      moveSupport,
      pagination:
        undefined,
    }),
    [
      content,
      error,
      fileOperations,
      fileSelection.selectionMode,
      fileSelection.selectedIds,
      handleToggleItem,
      handleGoUp,
      folderOperations,
      folderOps.handleCancelNewFolder,
      folderOps.handleConfirmNewFolder,
      folderOps.newFolderName,
      folderOps.setNewFolderName,
      isContentTransitioning,
      isCreatingInThisFolder,
      layoutType,
      moveSupport,
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
        {error && (
          <Box mb={1} px={1}>
            <Alert severity="error">{error}</Alert>
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
