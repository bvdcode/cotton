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
import { FileBreadcrumbs, FileListViewFactory } from "../files/components";
import { ArrowUpward, ViewModule, ViewList, Delete } from "@mui/icons-material";
import { useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import Loader from "../../shared/ui/Loader";
import { nodesApi } from "../../shared/api/nodesApi";
import { layoutsApi, type NodeDto } from "../../shared/api/layoutsApi";
import type { NodeContentDto } from "../../shared/api/nodesApi";
import { MediaLightbox } from "../files/components";
import type { MediaItem } from "../files/components";
import { formatBytes } from "../files/utils/formatBytes";
import { isImageFile, isVideoFile } from "../files/utils/fileTypes";
import { getFileIcon } from "../files/utils/icons";
import {
  PreviewModal,
  PdfPreview,
  TextPreview,
} from "../files/components/preview";
import { useTrashFolderOperations } from "./hooks/useTrashFolderOperations";
import { useTrashFileOperations } from "./hooks/useTrashFileOperations";
import { useFilePreview } from "../files/hooks/useFilePreview";
import { filesApi } from "../../shared/api/filesApi";
import type {
  FileSystemTile,
  FolderOperations,
  FileOperations,
} from "../files/types/FileListViewTypes";
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

  const breadcrumbs = useMemo(() => {
    if (!currentNode) return [] as Array<{ id: string; name: string }>;
    const chain = [...ancestors, currentNode];
    return chain.map((n) => ({ id: n.id, name: n.name }));
  }, [ancestors, currentNode]);

  const sortedFolders = useMemo(() => {
    const nodes = (content?.nodes ?? []).slice();
    nodes.sort((a, b) =>
      a.name.localeCompare(b.name, undefined, { numeric: true }),
    );
    return nodes;
  }, [content?.nodes]);

  const sortedFiles = useMemo(() => {
    const files = (content?.files ?? []).slice();
    files.sort((a, b) =>
      a.name.localeCompare(b.name, undefined, { numeric: true }),
    );
    return files;
  }, [content?.files]);

  // Build tiles array for view components
  const tiles = useMemo<FileSystemTile[]>(() => {
    return [
      ...sortedFolders.map((node) => ({ kind: "folder", node }) as const),
      ...sortedFiles.map((file) => ({ kind: "file", file }) as const),
    ];
  }, [sortedFolders, sortedFiles]);

  const folderOps = useTrashFolderOperations(nodeId, refreshContent);
  const fileOps = useTrashFileOperations(refreshContent);
  const { previewState, openPreview, closePreview } = useFilePreview();

  // Media lightbox state
  const [lightboxOpen, setLightboxOpen] = React.useState(false);
  const [lightboxIndex, setLightboxIndex] = React.useState(0);

  // Build media items for lightbox (images and videos only)
  const mediaItems = useMemo<MediaItem[]>(() => {
    return sortedFiles
      .filter((file) => isImageFile(file.name) || isVideoFile(file.name))
      .map((file) => {
        const preview = getFileIcon(
          file.encryptedFilePreviewHashHex ?? null,
          file.name,
        );
        const previewUrl = typeof preview === "string" ? preview : "";

        return {
          id: file.id,
          kind: isImageFile(file.name) ? "image" : "video",
          name: file.name,
          previewUrl,
          mimeType: file.name.toLowerCase().endsWith(".mp4")
            ? "video/mp4"
            : undefined,
          sizeBytes: file.sizeBytes,
        } as MediaItem;
      });
  }, [sortedFiles]);

  // Get signed media URL for original file
  const getSignedMediaUrl = async (fileId: string): Promise<string> => {
    return await filesApi.getDownloadLink(fileId, 60 * 24);
  };

  // Handler to open media lightbox
  const handleMediaClick = (fileId: string) => {
    const mediaIndex = mediaItems.findIndex((item) => item.id === fileId);
    if (mediaIndex !== -1) {
      setLightboxIndex(mediaIndex);
      setLightboxOpen(true);
    }
  };

  const stats = useMemo(() => {
    const folders = content?.nodes?.length ?? 0;
    const files = content?.files?.length ?? 0;
    const sizeBytes = (content?.files ?? []).reduce(
      (sum, file) => sum + (file.sizeBytes ?? 0),
      0,
    );
    return { folders, files, sizeBytes };
  }, [content?.files, content?.nodes]);

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
    try {
      const downloadLink = await filesApi.getDownloadLink(nodeFileId);
      const link = document.createElement("a");
      link.href = downloadLink;
      link.download = fileName;
      link.target = "_blank";
      link.rel = "noopener noreferrer";
      link.style.display = "none";
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    } catch (error) {
      console.error("Failed to download file:", error);
    }
  };

  const handleFileClick = (fileId: string, fileName: string) => {
    const opened = openPreview(fileId, fileName);
    if (!opened) {
      void handleDownloadFile(fileId, fileName);
    }
  };

  // Build folder operations adapter
  const folderOperations: FolderOperations = {
    isRenaming: (folderId: string) => folderOps.renamingFolderId === folderId,
    getRenamingName: () => folderOps.renamingFolderName,
    onRenamingNameChange: folderOps.setRenamingFolderName,
    onConfirmRename: folderOps.handleConfirmRename,
    onCancelRename: folderOps.handleCancelRename,
    onStartRename: folderOps.handleRenameFolder,
    onDelete: folderOps.handleDeleteFolder,
    onClick: (folderId: string) => navigate(`/trash/${folderId}`),
  };

  // Build file operations adapter
  const fileOperations: FileOperations = {
    isRenaming: (fileId: string) => fileOps.renamingFileId === fileId,
    getRenamingName: () => fileOps.renamingFileName,
    onRenamingNameChange: fileOps.setRenamingFileName,
    onConfirmRename: fileOps.handleConfirmRename,
    onCancelRename: fileOps.handleCancelRename,
    onStartRename: fileOps.handleRenameFile,
    onDelete: fileOps.handleDeleteFile,
    onDownload: handleDownloadFile,
    onClick: handleFileClick,
    onMediaClick: handleMediaClick,
  };

  const isCreatingInThisFolder = false; // No folder creation in trash

  if (loading && !content) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

  return (
    <>
      <Box width="100%" sx={{ position: "relative" }}>
        <Box
          sx={{
            position: "sticky",
            top: 0,
            zIndex: 20,
            bgcolor: "background.default",
            display: "flex",
            flexDirection: "column",
            marginBottom: 2,
            borderBottom: 1,
            borderColor: "divider",
            paddingTop: 1,
            paddingBottom: 1,
          }}
        >
          {loading && (
            <LinearProgress
              sx={{
                position: "absolute",
                top: 0,
                left: 0,
                right: 0,
              }}
            />
          )}
          <Box
            sx={{
              display: "flex",
              flexDirection: { xs: "column", sm: "row" },
              gap: { xs: 1, sm: 1 },
              alignItems: { xs: "stretch", sm: "center" },
            }}
          >
            <Box
              sx={{
                display: "flex",
                gap: 1,
                alignItems: "center",
                justifyContent: "space-between",
              }}
            >
              <Box
                sx={{
                  display: "flex",
                  gap: 0.5,
                  alignItems: "center",
                  flexShrink: 0,
                }}
              >
                <IconButton
                  color="primary"
                  onClick={handleGoUp}
                  disabled={loading || ancestors.length === 0}
                  title={t("actions.goUp")}
                >
                  <ArrowUpward />
                </IconButton>
                {ancestors.length === 0 ? (
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
                ) : (
                  <IconButton
                    onClick={() => navigate("/trash")}
                    color="primary"
                    title={t("actions.trashRoot")}
                  >
                    <Delete />
                  </IconButton>
                )}
                {layoutType === InterfaceLayoutType.Tiles ? (
                  <IconButton
                    color="primary"
                    onClick={() => setLayoutType(InterfaceLayoutType.List)}
                    title={t("actions.switchToList")}
                  >
                    <ViewList />
                  </IconButton>
                ) : (
                  <IconButton
                    color="primary"
                    onClick={() => setLayoutType(InterfaceLayoutType.Tiles)}
                    title={t("actions.switchToTiles")}
                  >
                    <ViewModule />
                  </IconButton>
                )}
              </Box>

              <Box
                sx={{
                  display: { xs: "flex", sm: "none" },
                  flexShrink: 0,
                  whiteSpace: "nowrap",
                }}
              >
                <Typography
                  color="text.secondary"
                  sx={{ fontSize: "0.875rem" }}
                >
                  {t("stats.summary", {
                    folders: stats.folders,
                    files: stats.files,
                    size: formatBytes(stats.sizeBytes),
                  })}
                </Typography>
              </Box>
            </Box>

            <FileBreadcrumbs breadcrumbs={breadcrumbs} />

            <Box
              sx={{
                flexShrink: 0,
                whiteSpace: "nowrap",
                display: { xs: "none", sm: "block" },
                ml: "auto",
              }}
            >
              <Typography color="text.secondary" sx={{ fontSize: "0.875rem" }}>
                {t("stats.summary", {
                  folders: stats.folders,
                  files: stats.files,
                  size: formatBytes(stats.sizeBytes),
                })}
              </Typography>
            </Box>
          </Box>
        </Box>
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

      {previewState.isOpen && previewState.fileId && previewState.fileName && (
        <PreviewModal
          open={previewState.isOpen}
          onClose={closePreview}
          layout={previewState.fileType === "pdf" ? "header" : "overlay"}
          title={
            previewState.fileType === "pdf" ? previewState.fileName : undefined
          }
        >
          {previewState.fileType === "pdf" && (
            <PdfPreview
              fileId={previewState.fileId}
              fileName={previewState.fileName}
            />
          )}
          {previewState.fileType === "text" && (
            <TextPreview
              nodeFileId={previewState.fileId}
              fileName={previewState.fileName}
              onSaved={() => {
                // Optionally refresh trash content
              }}
            />
          )}
        </PreviewModal>
      )}

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
