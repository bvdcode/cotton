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
import { layoutsApi, type NodeDto } from "../../shared/api/layoutsApi";
import type { NodeContentDto } from "../../shared/api/nodesApi";
import { useTrashFolderOperations } from "./hooks/useTrashFolderOperations";
import { useTrashFileOperations } from "./hooks/useTrashFileOperations";
import { useFilePreview } from "../files/hooks/useFilePreview";
import { useMediaLightbox } from "../files/hooks/useMediaLightbox";
import { downloadFile } from "../files/utils/fileHandlers";
import { buildBreadcrumbs, calculateFolderStats } from "../files/utils/nodeUtils";
import { useContentTiles } from "../../shared/hooks/useContentTiles";
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import { filesApi } from "../../shared/api/filesApi";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { usePreferencesStore } from "../../shared/store/preferencesStore";

/**
 * TrashPage Component
 *
 * Page for browsing and managing files and folders in trash.
 * Similar to FilesPage but uses 'trash' nodeType for API calls.
 */
export const TrashPage: React.FC = () => {
  const { t } = useTranslation(["trash", "common"]);
  const navigate = useNavigate();
  const params = useParams<{ nodeId?: string }>();
  const confirm = useConfirm();

  const [currentNode, setCurrentNode] = React.useState<NodeDto | null>(null);
  const [ancestors, setAncestors] = React.useState<NodeDto[]>([]);
  const [content, setContent] = React.useState<NodeContentDto | undefined>(
    undefined,
  );
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

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

  const { layoutPreferences, setTrashLayoutType } = usePreferencesStore();

  // Empty trash progress state
  const [emptyingTrash, setEmptyingTrash] = React.useState(false);
  const [emptyTrashProgress, setEmptyTrashProgress] = React.useState({
    current: 0,
    total: 0,
  });

  const routeNodeId = params.nodeId;
  const initialLayoutType =
    layoutPreferences.trashLayoutType ?? InterfaceLayoutType.Tiles;

  const [layoutType, setLayoutType] =
    React.useState<InterfaceLayoutType>(initialLayoutType);

  // Load trash root or specific node
  useEffect(() => {
    const loadTrashData = async () => {
      setLoading(true);
      setError(null);

      const shouldLoadContent = layoutType !== InterfaceLayoutType.List;

      try {
        if (!routeNodeId) {
          // Load trash root
          const root = await layoutsApi.resolve({ nodeType: "trash" });
          const [nodeData, ancestorsData, contentData] = await Promise.all([
            nodesApi.getNode(root.id),
            nodesApi.getAncestors(root.id, { nodeType: "trash" }),
            shouldLoadContent
              ? nodesApi.getChildren(root.id, { nodeType: "trash" })
              : Promise.resolve(null),
          ]);

          setCurrentNode(nodeData);
          setAncestors(ancestorsData);
          setContent(contentData ? contentData.content : undefined);
        } else {
          // Load specific trash node
          const [nodeData, ancestorsData, contentData] = await Promise.all([
            nodesApi.getNode(routeNodeId),
            nodesApi.getAncestors(routeNodeId, { nodeType: "trash" }),
            shouldLoadContent
              ? nodesApi.getChildren(routeNodeId, { nodeType: "trash" })
              : Promise.resolve(null),
          ]);

          setCurrentNode(nodeData);
          setAncestors(ancestorsData);
          setContent(contentData ? contentData.content : undefined);
        }
      } catch (err) {
        console.error("Failed to load trash data:", err);
        setError(t("error"));
      } finally {
        setLoading(false);
      }
    };

    void loadTrashData();
  }, [routeNodeId, layoutType, t]);

  const nodeId = routeNodeId ?? currentNode?.id ?? null;

  const fetchListPage = React.useCallback(async () => {
    if (!nodeId) return;

    setListLoading(true);
    setListError(null);
    try {
      const response = await nodesApi.getChildren(nodeId, {
        nodeType: "trash",
        page: listPage + 1,
        pageSize: listPageSize,
      });
      setListContent(response.content);
      setListTotalCount(response.totalCount);
    } catch (err) {
      console.error("Failed to load paged trash content", err);
      setListError(t("error"));
    } finally {
      setListLoading(false);
    }
  }, [nodeId, listPage, listPageSize, t]);

  // Refresh current folder content
  const refreshContent = React.useCallback(async () => {
    if (!nodeId) return;

    try {
      if (layoutType === InterfaceLayoutType.List) {
        await fetchListPage();
        return;
      }

      const contentData = await nodesApi.getChildren(nodeId, {
        nodeType: "trash",
      });
      setContent(contentData.content);
    } catch (err) {
      console.error("Failed to refresh trash content:", err);
    }
  }, [nodeId, layoutType, fetchListPage]);

  // Reset paging when folder changes or switching to list view
  useEffect(() => {
    setListPage(0);
  }, [nodeId, layoutType]);

  useEffect(() => {
    setTrashLayoutType(layoutType);
  }, [layoutType, setTrashLayoutType]);

  useEffect(() => {
    if (layoutType !== InterfaceLayoutType.List) return;
    if (!nodeId) return;
    void fetchListPage();
  }, [layoutType, nodeId, fetchListPage]);

  // Update page title based on current folder
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

    // Cleanup: reset to default title when component unmounts
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

  const { sortedFiles, tiles } = useContentTiles(effectiveContent);

  const folderOps = useTrashFolderOperations(nodeId, refreshContent);
  const fileOps = useTrashFileOperations(refreshContent);
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

      // Delete all folders
      for (const folder of content.nodes ?? []) {
        try {
          await nodesApi.deleteNode(folder.id, true);
          deleted++;
          setEmptyTrashProgress({ current: deleted, total: totalItems });
        } catch (err) {
          console.error(`Failed to delete folder ${folder.id}:`, err);
        }
      }

      // Delete all files
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
      // User cancelled
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

  // Build folder operations adapter
  const folderOperations = buildFolderOperations(folderOps, (folderId) =>
    navigate(`/trash/${folderId}`),
  );

  // Build file operations adapter
  const fileOperations = buildFileOperations(fileOps, {
    onDownload: handleDownloadFile,
    onClick: handleFileClick,
    onMediaClick: handleMediaClick,
  });

  const isCreatingInThisFolder = false; // No folder creation in trash

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

        <Box
          ref={listGridHostRef}
          pb={{ xs: 2, sm: 3 }}
          sx={{ flex: 1, minHeight: 0 }}
        >
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
