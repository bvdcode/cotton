import React, { useDeferredValue, useEffect, useMemo } from "react";
import { Alert, Box, Typography } from "@mui/material";
import {
  FileListViewFactory,
  PageHeader,
  MediaLightbox,
  FilePreviewModal,
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

  const { layoutType, setLayoutType } = useFilesLayout();

  useEffect(() => {
    const loadChildren = layoutType !== InterfaceLayoutType.List;

    if (!routeNodeId) {
      void loadRoot({ force: false, loadChildren });
      return;
    }
    void loadNode(routeNodeId, { loadChildren });
  }, [routeNodeId, layoutType, loadRoot, loadNode]);

  const nodeId = routeNodeId ?? currentNode?.id ?? null;
  const content = nodeId ? contentByNodeId[nodeId] : undefined;

  const {
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

  // Build folder operations adapter
  const folderOperations = buildFolderOperations(folderOps, (folderId) =>
    navigate(`/files/${folderId}`),
  );

  // Build file operations adapter
  const fileOperations = buildFileOperations(fileOps, {
    onDownload: handleDownloadFile,
    onClick: handleFileClick,
    onMediaClick: handleMediaClick,
  });

  const isCreatingInThisFolder =
    folderOps.isCreatingFolder && folderOps.newFolderParentId === nodeId;

  return (
    <>
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
          minHeight: 0,
        }}
      >
        <PageHeader
          loading={loading}
          breadcrumbs={breadcrumbs}
          stats={stats}
          layoutType={layoutType}
          canGoUp={ancestors.length > 0}
          onGoUp={handleGoUp}
          onHomeClick={() => navigate("/files")}
          onLayoutToggle={setLayoutType}
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

        <Box sx={{ flex: 1, minHeight: 0 }}>
          <FileListViewFactory
            layoutType={layoutType}
            tiles={tiles}
            folderOperations={folderOperations}
            fileOperations={fileOperations}
            isCreatingFolder={isCreatingInThisFolder}
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
                    onPaginationModelChange: (model) => {
                      handlePaginationChange(model.page, model.pageSize);
                    },
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
    </>
  );
};
