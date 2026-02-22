import React, { useDeferredValue, useEffect, useMemo } from "react";
import { Alert, Box, Snackbar, Typography } from "@mui/material";
import {
  FileListViewFactory,
  PageHeader,
  MediaLightbox,
  FilePreviewModal,
  FileConflictDialog,
} from "./components";
import { useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useNodesStore } from "../../shared/store/nodesStore";
import { useFolderOperations } from "./hooks/useFolderOperations";
import { useFileUpload } from "./hooks/useFileUpload";
import { useFileOperations } from "./hooks/useFileOperations";
import { useFilePreview } from "./hooks/useFilePreview";
import { useMediaLightbox } from "./hooks/useMediaLightbox";
import { useFilesLayout } from "./hooks/useFilesLayout";
import { useFilesData } from "./hooks/useFilesData";
import { useFilesRealtimeEvents } from "./hooks/useFilesRealtimeEvents";
import { useFileSelection } from "./hooks/useFileSelection";
import { downloadFile } from "./utils/fileHandlers";
import { buildBreadcrumbs, calculateFolderStats } from "./utils/nodeUtils";
import { useContentTiles } from "../../shared/hooks/useContentTiles";
import { useFolderFileList } from "../../shared/hooks/useFileListSource";
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { shareFile } from "../../shared/utils/shareFile";
import Loader from "../../shared/ui/Loader";

const HUGE_FOLDER_THRESHOLD = 10_000;

type TranslationFn = (
  key: string,
  options?: {
    ns?: string;
    count?: number;
    processed?: number;
    total?: number;
  },
) => string;

type DropPreparationInfo = {
  phase: "idle" | "scanning" | "preparing";
  step: "idle" | "scanning" | "mapping" | "folders" | "conflicts" | "enqueue";
  filesFound: number;
  processed: number;
};

function getDropPreparationTitle(t: TranslationFn, info: DropPreparationInfo): string {
  const { phase, step } = info;

  if (phase === "scanning") {
    return t("uploadDrop.scanning.title", { ns: "files" });
  }

  if (step === "mapping") {
    return t("uploadDrop.preparing.mapping.title", { ns: "files" });
  }
  if (step === "folders") {
    return t("uploadDrop.preparing.folders.title", { ns: "files" });
  }
  if (step === "conflicts") {
    return t("uploadDrop.preparing.conflicts.title", { ns: "files" });
  }
  if (step === "enqueue") {
    return t("uploadDrop.preparing.enqueue.title", { ns: "files" });
  }

  return t("uploadDrop.preparing.title", { ns: "files" });
}

function getDropPreparationCaption(
  t: TranslationFn,
  info: DropPreparationInfo,
): string {
  const { phase, filesFound, processed } = info;

  const found = t("uploadDrop.captionFound", {
    ns: "files",
    count: filesFound,
  });

  if (phase === "scanning") return found;
  if (filesFound <= 0) return found;

  const progress = t("uploadDrop.captionProgress", {
    ns: "files",
    processed: Math.max(0, Math.min(filesFound, processed)),
    total: filesFound,
  });

  return `${found} • ${progress}`;
}

type ShareToastState = {
  open: boolean;
  message: string;
};

type ShareToastSnackbarProps = {
  toast: ShareToastState;
  onClose: () => void;
};

const ShareToastSnackbar: React.FC<ShareToastSnackbarProps> = ({
  toast,
  onClose,
}) => {
  return (
    <Snackbar
      open={toast.open}
      autoHideDuration={2500}
      onClose={onClose}
      message={toast.message}
    />
  );
};

type DraggingOverlayProps = {
  open: boolean;
  onDragEnter: React.DragEventHandler<HTMLDivElement>;
  onDragOver: React.DragEventHandler<HTMLDivElement>;
  onDragLeave: React.DragEventHandler<HTMLDivElement>;
  onDrop: React.DragEventHandler<HTMLDivElement>;
  label: string;
};

const DraggingOverlay: React.FC<DraggingOverlayProps> = ({
  open,
  onDragEnter,
  onDragOver,
  onDragLeave,
  onDrop,
  label,
}) => {
  if (!open) return null;

  return (
    <Box
      onDragEnter={onDragEnter}
      onDragOver={onDragOver}
      onDragLeave={onDragLeave}
      onDrop={onDrop}
      sx={{
        position: "fixed",
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        bgcolor: "primary.main",
        opacity: 0.15,
        border: "4px dashed",
        borderColor: "primary.main",
        zIndex: 9999,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      <Typography
        variant="h3"
        sx={{
          color: "primary.main",
          fontWeight: "bold",
          textShadow: "0 0 10px rgba(255,255,255,0.8)",
          pointerEvents: "none",
        }}
      >
        {label}
      </Typography>
    </Box>
  );
};

export const FilesPage: React.FC = () => {
  const { t } = useTranslation(["files", "common"]);
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

  useFolderFileList({
    nodeId,
    layoutType,
    listContent,
  });

  const { sortedFiles, tiles } = useContentTiles(deferredContent ?? undefined);

  const [shareToast, setShareToast] = React.useState<ShareToastState>({
    open: false,
    message: "",
  });

  const showToast = React.useCallback(
    (message: string) => setShareToast({ open: true, message }),
    [],
  );

  const folderOps = useFolderOperations(nodeId, handleFolderChanged);
  const fileUpload = useFileUpload(nodeId, breadcrumbs, content, {
    onToast: showToast,
  });
  const fileOps = useFileOperations(reloadCurrentNode);
  const { previewState, openPreview, closePreview } = useFilePreview();
  const fileSelection = useFileSelection();

  const {
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    handleMediaClick,
    setLightboxOpen,
  } = useMediaLightbox(sortedFiles);

  const stats = useMemo(
    () => calculateFolderStats(deferredContent?.nodes, deferredContent?.files),
    [deferredContent?.files, deferredContent?.nodes],
  );

  const goToFolder = useMemo(
    () => (folderId: string) => navigate(`/files/${folderId}`),
    [navigate],
  );

  const goHome = useMemo(() => () => navigate("/files"), [navigate]);

  const handleGoUp = () => {
    if (ancestors.length > 0) {
      const parent = ancestors[ancestors.length - 1];
      navigate(`/files/${parent.id}`);
    } else {
      navigate("/files");
    }
  };

  const handleDownloadFile = async (nodeFileId: string, fileName: string) => {
    await downloadFile(nodeFileId, fileName);
  };

  const handleShareFile = React.useCallback(
    async (nodeFileId: string, fileName: string) => {
      await shareFile(nodeFileId, fileName, t, setShareToast);
    },
    [t],
  );

  const handleFileClick = (
    fileId: string,
    fileName: string,
    fileSizeBytes?: number,
  ) => {
    const opened = openPreview(fileId, fileName, fileSizeBytes);
    if (!opened) {
      void handleDownloadFile(fileId, fileName);
    }
  };

  const onPaginationModelChange = useMemo(
    () => (model: { page: number; pageSize: number }) => {
      handlePaginationChange(model.page, model.pageSize);
    },
    [handlePaginationChange],
  );

  // Build folder operations adapter
  const folderOperations = buildFolderOperations(folderOps, goToFolder);

  // Build file operations adapter
  const fileOperations = buildFileOperations(fileOps, {
    onDownload: handleDownloadFile,
    onShare: handleShareFile,
    onClick: handleFileClick,
    onMediaClick: handleMediaClick,
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
      onToggleSelectionMode: fileSelection.toggleSelectionMode,
      onSelectAll: () => fileSelection.selectAll(tiles),
      onDeselectAll: fileSelection.deselectAll,
    }),
    [
      ancestors.length,
      breadcrumbs,
      cycleViewMode,
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
      onToggleItem: fileSelection.toggleItem,
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
      fileSelection.toggleItem,
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

      <ShareToastSnackbar
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
