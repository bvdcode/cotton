import React, { useEffect, useMemo } from "react";
import {
  Alert,
  Box,
  IconButton,
  LinearProgress,
  Dialog,
  DialogContent,
  DialogTitle,
} from "@mui/material";
import {
  FileListViewFactory,
  PageHeader,
  MediaLightbox,
  FilePreviewModal,
} from "../files/components";
import { Delete } from "@mui/icons-material";
import { useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import Loader from "../../shared/ui/Loader";
import { nodesApi } from "../../shared/api/nodesApi";
import type { NodeContentDto } from "../../shared/api/nodesApi";
import { useTrashStore } from "../../shared/store/trashStore";
import { useTrashFolderOperations } from "./hooks/useTrashFolderOperations";
import { useTrashFileOperations } from "./hooks/useTrashFileOperations";
import { useFilePreview } from "../files/hooks/useFilePreview";
import { useMediaLightbox } from "../files/hooks/useMediaLightbox";
import { downloadFile } from "../files/utils/fileHandlers";
import { buildBreadcrumbs, calculateFolderStats } from "../files/utils/nodeUtils";
import { useContentTiles } from "../../shared/hooks/useContentTiles";
import { useTrashFileList } from "../../shared/hooks/useFileListSource";
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import { filesApi } from "../../shared/api/filesApi";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { usePreferencesStore } from "../../shared/store/preferencesStore";

export const TrashPage: React.FC = () => {
  const { t } = useTranslation(["trash", "common"]);
  const navigate = useNavigate();
  const params = useParams<{ nodeId?: string }>();
  const confirm = useConfirm();

  const {
    currentNode,
    ancestors,
    contentByNodeId,
    loading,
    error,
    loadRoot,
    loadNode,
    refreshNodeContent,
  } = useTrashStore();

  const routeNodeId = params.nodeId;

  const { layoutPreferences, setTrashLayoutType } = usePreferencesStore();
  const initialLayoutType =
    layoutPreferences.trashLayoutType ?? InterfaceLayoutType.Tiles;
  const [layoutType, setLayoutType] =
    React.useState<InterfaceLayoutType>(initialLayoutType);

  const [listTotalCount, setListTotalCount] = React.useState(0);
  const [listLoading, setListLoading] = React.useState(false);
  const [listError, setListError] = React.useState<string | null>(null);
  const [listContent, setListContent] = React.useState<NodeContentDto | null>(null);
  const [currentPagination, setCurrentPagination] = React.useState<{ page: number; pageSize: number } | null>(null);

  const [emptyingTrash, setEmptyingTrash] = React.useState(false);
  const [emptyTrashProgress, setEmptyTrashProgress] = React.useState({
    current: 0,
    total: 0,
  });

  useEffect(() => {
    const loadChildren = layoutType !== InterfaceLayoutType.List;
    if (!routeNodeId) {
      void loadRoot({ force: false, loadChildren });
    } else {
      void loadNode(routeNodeId, { loadChildren });
    }
  }, [routeNodeId, layoutType, loadRoot, loadNode]);

  const nodeId = routeNodeId ?? currentNode?.id ?? null;
  const content = nodeId ? contentByNodeId[nodeId] : undefined;

  const fetchListPage = React.useCallback(async (page: number, pageSize: number) => {
    if (!nodeId) return;

    setListLoading(true);
    setListError(null);
    try {
      const response = await nodesApi.getChildren(nodeId, {
        nodeType: "trash",
        page: page + 1,
        pageSize,
      });
      setListContent(response.content);
      setListTotalCount(response.totalCount);
    } catch (err) {
      console.error("Failed to load paged trash content", err);
      setListError(t("error"));
    } finally {
      setListLoading(false);
    }
  }, [nodeId, t]);

  useEffect(() => {
    if (layoutType === InterfaceLayoutType.List && nodeId && currentPagination) {
      void fetchListPage(currentPagination.page, currentPagination.pageSize);
    }
  }, [nodeId, layoutType, currentPagination, fetchListPage]);

  const handlePaginationChange = React.useCallback((page: number, pageSize: number) => {
    setCurrentPagination({ page, pageSize });
    void fetchListPage(page, pageSize);
  }, [fetchListPage]);  const refreshContent = React.useCallback(async () => {
    if (!nodeId) return;
    void refreshNodeContent(nodeId);
  }, [nodeId, refreshNodeContent]);

  useEffect(() => {
    setTrashLayoutType(layoutType);
  }, [layoutType, setTrashLayoutType]);



  useEffect(() => {
    const folderName = currentNode?.name;
    const isRoot = !routeNodeId || ancestors.length === 0;

    if (isRoot) {
      document.title = "Cotton - Trash";
    } else if (folderName) {
      document.title = `Cotton - ${folderName}`;
    } else {
      document.title = "Cotton";
    }

    return () => {
      document.title = "Cotton";
    };
  }, [currentNode?.name, routeNodeId, ancestors.length]);

  const breadcrumbs = useMemo(
    () => buildBreadcrumbs(ancestors, currentNode),
    [ancestors, currentNode],
  );

  const effectiveContent =
    layoutType === InterfaceLayoutType.List
      ? (listContent ?? content)
      : content;

  useTrashFileList({
    nodeId,
    layoutType,
    listContent,
  });

  const { sortedFiles, tiles } = useContentTiles(effectiveContent);

  const folderOps = useTrashFolderOperations(nodeId, refreshContent);
  const fileOps = useTrashFileOperations(refreshContent);
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
    () => calculateFolderStats(content?.nodes, content?.files),
    [content?.files, content?.nodes],
  );

  const handleGoUp = () => {
    if (ancestors.length > 0) {
      const parent = ancestors[ancestors.length - 1];
      navigate(`/trash/${parent.id}`);
    } else {
      navigate("/trash");
    }
  };

  const handleEmptyTrash = async () => {
    if (!content) return;

    const totalItems =
      (content.nodes?.length ?? 0) + (content.files?.length ?? 0);
    if (totalItems === 0) return;

    try {
      const result = await confirm({
        title: t("emptyTrash.confirmTitle"),
        description: t("emptyTrash.confirmDescription"),
        confirmationText: t("common:actions.delete"),
        cancellationText: t("common:actions.cancel"),
        confirmationButtonProps: { color: "error" },
      });

      if (!result.confirmed) return;

      setEmptyingTrash(true);
      setEmptyTrashProgress({ current: 0, total: totalItems });

      let deleted = 0;

      for (const folder of content.nodes ?? []) {
        try {
          await nodesApi.deleteNode(folder.id, true);
          deleted++;
          setEmptyTrashProgress({ current: deleted, total: totalItems });
        } catch (err) {
          console.error(`Failed to delete folder ${folder.id}:`, err);
        }
      }

      for (const file of content.files ?? []) {
        try {
          await filesApi.deleteFile(file.id, true);
          deleted++;
          setEmptyTrashProgress({ current: deleted, total: totalItems });
        } catch (err) {
          console.error(`Failed to delete file ${file.id}:`, err);
        }
      }

      setEmptyingTrash(false);
      await refreshContent();
    } catch {
      setEmptyingTrash(false);
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

  const folderOperations = buildFolderOperations(folderOps, (folderId) =>
    navigate(`/trash/${folderId}`),
  );

  const fileOperations = buildFileOperations(fileOps, {
    onDownload: handleDownloadFile,
    onClick: handleFileClick,
    onMediaClick: handleMediaClick,
  });

  const isCreatingInThisFolder = false;

  if (loading && !content && layoutType !== InterfaceLayoutType.List) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

  return (
    <>
      <Box
        width="100%"
        sx={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          flex: 1,
          minHeight: 0,
        }}
      >
        <PageHeader
          loading={layoutType !== InterfaceLayoutType.List && loading}
          breadcrumbs={breadcrumbs}
          stats={stats}
          layoutType={layoutType}
          canGoUp={ancestors.length > 0}
          onGoUp={handleGoUp}
          onHomeClick={() => navigate("/trash")}
          onLayoutToggle={setLayoutType}
          statsNamespace="trash"
          customActions={
            ancestors.length === 0 ? (
              <IconButton
                onClick={handleEmptyTrash}
                color="error"
                disabled={
                  loading ||
                  emptyingTrash ||
                  stats.folders + stats.files === 0
                }
                title={t("actions.emptyTrash")}
              >
                <Delete />
              </IconButton>
            ) : null
          }
          t={t}
        />
        {(error || listError) && (
          <Box mb={1} px={1}>
            <Alert severity="error">{error ?? listError}</Alert>
          </Box>
        )}

        <Box pb={{ xs: 2, sm: 3 }} sx={{ flex: 1, minHeight: 0 }}>
          <FileListViewFactory
            layoutType={layoutType}
            tiles={tiles}
            folderOperations={folderOperations}
            fileOperations={fileOperations}
            isCreatingFolder={isCreatingInThisFolder}
            emptyStateText={
              layoutType === InterfaceLayoutType.Tiles ? t("empty") : undefined
            }
            newFolderName=""
            onNewFolderNameChange={() => {}}
            onConfirmNewFolder={() => Promise.resolve()}
            onCancelNewFolder={() => {}}
            folderNamePlaceholder=""
            fileNamePlaceholder="File name"
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

      {emptyingTrash && (
        <Dialog open={emptyingTrash} disableEscapeKeyDown>
          <DialogTitle>
            {t("emptyTrash.inProgress", {
              current: emptyTrashProgress.current,
              total: emptyTrashProgress.total,
            })}
          </DialogTitle>
          <DialogContent>
            <LinearProgress
              variant="determinate"
              value={
                (emptyTrashProgress.current / emptyTrashProgress.total) * 100
              }
            />
          </DialogContent>
        </Dialog>
      )}
    </>
  );
};
