import React, { useDeferredValue, useEffect, useMemo, useRef } from "react";
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
import { downloadFile } from "./utils/fileHandlers";
import { buildBreadcrumbs, calculateFolderStats } from "./utils/nodeUtils";
import { useContentTiles } from "../../shared/hooks/useContentTiles";
import { useFolderFileList } from "../../shared/hooks/useFileListSource";
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { filesApi } from "../../shared/api/filesApi";
import { shareLinks } from "../../shared/utils/shareLinks";
import Loader from "../../shared/ui/Loader";

export const FilesPage: React.FC = () => {
  const { t } = useTranslation(["files", "common"]);
  const navigate = useNavigate();
  const params = useParams<{ nodeId?: string }>();

  const {
    currentNode,
    ancestors,
    contentByNodeId,
    loading,
    error,
    loadRoot,
    loadNode,
    refreshNodeContent,
  } = useNodesStore();

  const routeNodeId = params.nodeId;

  const { layoutType, setLayoutType, tilesSize, viewMode, cycleViewMode } =
    useFilesLayout();

  useEffect(() => {
    // Always load node metadata/ancestors fast; decide children loading separately.
    if (!routeNodeId) {
      void loadRoot({ force: false, loadChildren: false });
      return;
    }
    void loadNode(routeNodeId, { loadChildren: false });
  }, [routeNodeId, loadRoot, loadNode]);

  const nodeId = routeNodeId ?? currentNode?.id ?? null;
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
  } = useFilesData({
    nodeId,
    layoutType,
    loadRoot,
    loadNode,
    refreshNodeContent,
  });

  const HUGE_FOLDER_THRESHOLD = 10_000;
  const isHugeFolder =
    childrenTotalCount !== null && childrenTotalCount > HUGE_FOLDER_THRESHOLD;

  const loadedChildrenNodeIdRef = useRef<string | null>(null);

  useEffect(() => {
    if (!isHugeFolder) return;
    if (layoutType === InterfaceLayoutType.List) return;
    setLayoutType(InterfaceLayoutType.List);
  }, [isHugeFolder, layoutType, setLayoutType]);

  useEffect(() => {
    if (layoutType !== InterfaceLayoutType.Tiles) return;
    if (!nodeId) return;
    if (childrenTotalCount === null) return;
    if (childrenTotalCount > HUGE_FOLDER_THRESHOLD) return;

    if (loadedChildrenNodeIdRef.current === nodeId) return;
    loadedChildrenNodeIdRef.current = nodeId;

    void loadNode(nodeId, { loadChildren: true });
  }, [
    layoutType,
    nodeId,
    childrenTotalCount,
    loadNode,
    HUGE_FOLDER_THRESHOLD,
  ]);

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

  const folderOps = useFolderOperations(nodeId, handleFolderChanged);
  const fileUpload = useFileUpload(nodeId, breadcrumbs, content);
  const fileOps = useFileOperations(reloadCurrentNode);
  const { previewState, openPreview, closePreview } = useFilePreview();

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

  const [shareToast, setShareToast] = React.useState<{
    open: boolean;
    message: string;
  }>({ open: false, message: "" });

  const handleShareFile = React.useCallback(
    async (nodeFileId: string, fileName: string) => {
      try {
        const downloadLink = await filesApi.getDownloadLink(
          nodeFileId,
          60 * 24 * 365,
        );

        const token = shareLinks.tryExtractTokenFromDownloadUrl(downloadLink);
        if (!token) {
          setShareToast({
            open: true,
            message: t("share.errors.token", { ns: "files" }),
          });
          return;
        }

        const url = shareLinks.buildShareUrl(token);

        if (typeof navigator !== "undefined" && typeof navigator.share === "function") {
          try {
            await navigator.share({ title: fileName, url });
            setShareToast({
              open: true,
              message: t("share.shared", { ns: "files", name: fileName }),
            });
            return;
          } catch (e) {
            if (e instanceof Error && e.name === "AbortError") {
              return;
            }
          }
        }

        try {
          await navigator.clipboard.writeText(url);
          setShareToast({
            open: true,
            message: t("share.copied", { ns: "files", name: fileName }),
          });
        } catch {
          setShareToast({
            open: true,
            message: t("share.errors.copy", { ns: "files" }),
          });
        }
      } catch {
        setShareToast({
          open: true,
          message: t("share.errors.link", { ns: "files" }),
        });
      }
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

  return (
    <>
      {fileUpload.dropPreparation.active && (
        <Loader
          overlay
          title={(() => {
            const { phase, step } = fileUpload.dropPreparation;
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
          })()}
          caption={(() => {
            const { phase, filesFound, processed } = fileUpload.dropPreparation;
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
          })()}
        />
      )}
      <Snackbar
        open={shareToast.open}
        autoHideDuration={2500}
        onClose={() => setShareToast((prev) => ({ ...prev, open: false }))}
        message={shareToast.message}
      />
      {fileUpload.isDragging && (
        <Box
          onDragOver={fileUpload.handleDragOver}
          onDragLeave={fileUpload.handleDragLeave}
          onDrop={fileUpload.handleDrop}
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
            {t("actions.dropFiles")}
          </Typography>
        </Box>
      )}
      <Box
        width="100%"
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
          loading={loading}
          breadcrumbs={breadcrumbs}
          stats={stats}
          viewMode={viewMode}
          canGoUp={ancestors.length > 0}
          onGoUp={handleGoUp}
          onHomeClick={goHome}
          onViewModeCycle={cycleViewMode}
          showViewModeToggle={!isHugeFolder}
          showUpload={!!nodeId}
          showNewFolder={!!nodeId}
          onUploadClick={fileUpload.handleUploadClick}
          onNewFolderClick={folderOps.handleNewFolder}
          isCreatingFolder={folderOps.isCreatingFolder}
          t={t}
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
          <FileListViewFactory
            layoutType={layoutType}
            tiles={tiles}
            folderOperations={folderOperations}
            fileOperations={fileOperations}
            isCreatingFolder={isCreatingInThisFolder}
            tileSize={tilesSize}
            loading={
              layoutType === InterfaceLayoutType.List
                ? listLoading && !listContent
                : (loading && !content) || isContentTransitioning
            }
            loadingTitle={t("loading.title")}
            loadingCaption={t("loading.caption")}
            emptyStateText={
              layoutType === InterfaceLayoutType.Tiles
                ? t("empty.all")
                : undefined
            }
            newFolderName={folderOps.newFolderName}
            onNewFolderNameChange={folderOps.setNewFolderName}
            onConfirmNewFolder={folderOps.handleConfirmNewFolder}
            onCancelNewFolder={folderOps.handleCancelNewFolder}
            folderNamePlaceholder={t("actions.folderNamePlaceholder")}
            fileNamePlaceholder={t("rename.fileNamePlaceholder", {
              ns: "files",
            })}
            pagination={
              layoutType === InterfaceLayoutType.List
                ? {
                    totalCount: listTotalCount,
                    loading: listLoading,
                    onPaginationModelChange,
                  }
                : undefined
            }
          />
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
