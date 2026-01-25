import React, { useEffect, useMemo } from "react";
import {
  Alert,
  Box,
  IconButton,
  LinearProgress,
  Typography,
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

  // Empty trash progress state
  const [emptyingTrash, setEmptyingTrash] = React.useState(false);
  const [emptyTrashProgress, setEmptyTrashProgress] = React.useState({
    current: 0,
    total: 0,
  });

  const routeNodeId = params.nodeId;

  // Load trash root or specific node
  useEffect(() => {
    const loadTrashData = async () => {
      setLoading(true);
      setError(null);

      try {
        if (!routeNodeId) {
          // Load trash root
          const root = await layoutsApi.resolve({ nodeType: "trash" });
          const [nodeData, ancestorsData, contentData] = await Promise.all([
            nodesApi.getNode(root.id),
            nodesApi.getAncestors(root.id, { nodeType: "trash" }),
            nodesApi.getChildren(root.id, { nodeType: "trash" }),
          ]);

          setCurrentNode(nodeData);
          setAncestors(ancestorsData);
          setContent(contentData.content);
        } else {
          // Load specific trash node
          const [nodeData, ancestorsData, contentData] = await Promise.all([
            nodesApi.getNode(routeNodeId),
            nodesApi.getAncestors(routeNodeId, { nodeType: "trash" }),
            nodesApi.getChildren(routeNodeId, { nodeType: "trash" }),
          ]);

          setCurrentNode(nodeData);
          setAncestors(ancestorsData);
          setContent(contentData.content);
        }
      } catch (err) {
        console.error("Failed to load trash data:", err);
        setError(t("error"));
      } finally {
        setLoading(false);
      }
    };

    void loadTrashData();
  }, [routeNodeId, t]);

  const nodeId = routeNodeId ?? currentNode?.id ?? null;

  // Refresh current folder content
  const refreshContent = React.useCallback(async () => {
    if (!nodeId) return;

    try {
      const contentData = await nodesApi.getChildren(nodeId, {
        nodeType: "trash",
      });
      setContent(contentData.content);
    } catch (err) {
      console.error("Failed to refresh trash content:", err);
    }
  }, [nodeId]);

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

  const { sortedFiles, tiles } = useContentTiles(content);

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

  const handleFileClick = (fileId: string, fileName: string) => {
    const opened = openPreview(fileId, fileName);
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

  if (loading && !content) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

  return (
    <>
      <Box width="100%" sx={{ position: "relative" }}>
        <PageHeader
          loading={loading}
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
        {error && (
          <Box mb={1} px={1}>
            <Alert severity="error">{error}</Alert>
          </Box>
        )}

        <Box pb={{ xs: 2, sm: 3 }}>
          {tiles.length === 0 && !isCreatingInThisFolder ? (
            <Typography color="text.secondary">{t("empty")}</Typography>
          ) : (
            <FileListViewFactory
              layoutType={layoutType}
              tiles={tiles}
              folderOperations={folderOperations}
              fileOperations={fileOperations}
              isCreatingFolder={isCreatingInThisFolder}
              newFolderName=""
              onNewFolderNameChange={() => {}}
              onConfirmNewFolder={() => Promise.resolve()}
              onCancelNewFolder={() => {}}
              folderNamePlaceholder=""
              fileNamePlaceholder="File name"
            />
          )}
        </Box>
      </Box>

      <FilePreviewModal
        isOpen={previewState.isOpen}
        fileId={previewState.fileId}
        fileName={previewState.fileName}
        fileType={previewState.fileType}
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
