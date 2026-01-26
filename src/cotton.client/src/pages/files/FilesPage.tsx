import React, { useEffect, useMemo } from "react";
import { Alert, Box, Typography } from "@mui/material";
import {
  FileListViewFactory,
  PageHeader,
  MediaLightbox,
  FilePreviewModal,
} from "./components";
import { useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import Loader from "../../shared/ui/Loader";
import { useNodesStore } from "../../shared/store/nodesStore";
import { useFolderOperations } from "./hooks/useFolderOperations";
import { useFileUpload } from "./hooks/useFileUpload";
import { useFileOperations } from "./hooks/useFileOperations";
import { useFilePreview } from "./hooks/useFilePreview";
import { useMediaLightbox } from "./hooks/useMediaLightbox";
import { downloadFile } from "./utils/fileHandlers";
import { buildBreadcrumbs, calculateFolderStats } from "./utils/nodeUtils";
import { useContentTiles } from "../../shared/hooks/useContentTiles";
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { nodesApi, type NodeContentDto } from "../../shared/api/nodesApi";

/**
 * FilesPage Component
 *
 * Main page for browsing and managing files and folders.
 * Refactored to follow SOLID principles:
 * - Single Responsibility: Manages page state and coordinates child components
 * - Open/Closed: Can be extended with new layout types without modification
 * - Liskov Substitution: View components are interchangeable via interface
 * - Interface Segregation: View components depend only on needed operations
 * - Dependency Inversion: Depends on abstractions (IFileListView) not concrete implementations
 */
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

  useEffect(() => {
    if (!routeNodeId) {
      void loadRoot({ force: false });
      return;
    }
    void loadNode(routeNodeId);
  }, [routeNodeId, loadRoot, loadNode]);

  const nodeId = routeNodeId ?? currentNode?.id ?? null;
  const content = nodeId ? contentByNodeId[nodeId] : undefined;

  // List view (paged) state
  const [listPage, setListPage] = React.useState(0);
  const [listPageSize, setListPageSize] = React.useState(25);
  const [listTotalCount, setListTotalCount] = React.useState(0);
  const [listLoading, setListLoading] = React.useState(false);
  const [listError, setListError] = React.useState<string | null>(null);
  const [listContent, setListContent] = React.useState<NodeContentDto | null>(
    null,
  );
  const listGridHostRef = React.useRef<HTMLDivElement | null>(null);

  // Determine layout type from current node, defaulting to Tiles
  const initialLayoutType = useMemo(() => {
    return currentNode?.interfaceLayoutType ?? InterfaceLayoutType.Tiles;
  }, [currentNode?.interfaceLayoutType]);

  // Layout type state - can be changed by user
  const [layoutType, setLayoutType] =
    React.useState<InterfaceLayoutType>(initialLayoutType);

  // Sync layout type when node changes
  useEffect(() => {
    setLayoutType(initialLayoutType);
  }, [initialLayoutType]);

  // Reset paging when folder changes or switching to list view
  useEffect(() => {
    setListPage(0);
  }, [nodeId, layoutType]);

  // Avoid showing previous folder's list while navigating
  useEffect(() => {
    if (layoutType !== InterfaceLayoutType.List) return;
    setListTotalCount(0);
    setListError(null);
  }, [nodeId, layoutType]);

  // Update page title based on current folder
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

    // Cleanup: reset to default title when component unmounts
    return () => {
      document.title = "Cotton";
    };
  }, [currentNode?.name, routeNodeId, ancestors.length, t]);

  const breadcrumbs = useMemo(
    () => buildBreadcrumbs(ancestors, currentNode),
    [ancestors, currentNode],
  );

  const effectiveContent =
    layoutType === InterfaceLayoutType.List
      ? (listContent ?? content)
      : content;

  const { sortedFiles, tiles } = useContentTiles(effectiveContent ?? undefined);

  const fetchListPage = React.useCallback(async () => {
    if (!nodeId) return;
    setListLoading(true);
    setListError(null);
    try {
      const response = await nodesApi.getChildren(nodeId, {
        page: listPage + 1,
        pageSize: listPageSize,
      });
      setListContent(response.content);
      setListTotalCount(response.totalCount);
    } catch (err) {
      console.error("Failed to load paged content", err);
      setListError(t("error"));
    } finally {
      setListLoading(false);
    }
  }, [nodeId, listPage, listPageSize, t]);

  useEffect(() => {
    if (layoutType !== InterfaceLayoutType.List) return;
    if (!nodeId) return;
    void fetchListPage();
  }, [layoutType, nodeId, fetchListPage]);

  const folderOps = useFolderOperations(nodeId);
  const fileUpload = useFileUpload(nodeId, breadcrumbs, content);
  const fileOps = useFileOperations(() => {
    if (!nodeId) return;
    // Reload current folder after file operation
    if (layoutType === InterfaceLayoutType.List) {
      void fetchListPage();
    } else {
      void loadNode(nodeId);
    }
  });
  const { previewState, openPreview, closePreview } = useFilePreview();

  // Media lightbox management
  const {
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    handleMediaClick,
    setLightboxOpen,
  } = useMediaLightbox(sortedFiles);

  const stats = useMemo(
    () => calculateFolderStats(content?.nodes, content?.files),
    [content?.files, content?.nodes],
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

  const handleFileClick = (fileId: string, fileName: string, fileSizeBytes?: number) => {
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

  if (loading && !content && layoutType !== InterfaceLayoutType.List) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

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

        <Box ref={listGridHostRef} pb={1} sx={{ flex: 1, minHeight: 0 }}>
          {tiles.length === 0 &&
          !isCreatingInThisFolder &&
          !(layoutType === InterfaceLayoutType.List && listLoading) ? (
            <Typography color="text.secondary">{t("empty.all")}</Typography>
          ) : (
            <FileListViewFactory
              layoutType={layoutType}
              tiles={tiles}
              folderOperations={folderOperations}
              fileOperations={fileOperations}
              isCreatingFolder={isCreatingInThisFolder}
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
                      page: listPage,
                      pageSize: listPageSize,
                      totalCount: listTotalCount,
                      loading: listLoading,
                      onPageChange: (newPage) => {
                        setListPage(newPage);
                      },
                      onPageSizeChange: (newPageSize) => {
                        setListPageSize(newPageSize);
                        setListPage(0);
                      },
                    }
                  : undefined
              }
            />
          )}
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
