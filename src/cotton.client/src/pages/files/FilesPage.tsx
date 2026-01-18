import React, { useEffect, useMemo } from "react";
import {
  Alert,
  Box,
  Divider,
  IconButton,
  LinearProgress,
  Typography,
} from "@mui/material";
import { FileBreadcrumbs, FileListViewFactory } from "./components";
import {
  ArrowUpward,
  CreateNewFolder,
  Home,
  UploadFile,
  ViewModule,
  ViewList,
} from "@mui/icons-material";
import { useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import Loader from "../../shared/ui/Loader";
import { useNodesStore } from "../../shared/store/nodesStore";
import { filesApi } from "../../shared/api/filesApi";
import { MediaLightbox } from "./components";
import type { MediaItem } from "./components";
import { formatBytes } from "./utils/formatBytes";
import { isImageFile, isVideoFile } from "./utils/fileTypes";
import { getFilePreview } from "./utils/getFilePreview";
import { PreviewModal, PdfPreview } from "./components/preview";
import { useFolderOperations } from "./hooks/useFolderOperations";
import { useFileUpload } from "./hooks/useFileUpload";
import { useFileOperations } from "./hooks/useFileOperations";
import { useFilePreview } from "./hooks/useFilePreview";
import type {
  FileSystemTile,
  FolderOperations,
  FileOperations,
} from "./types/FileListViewTypes";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";

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

  const folderOps = useFolderOperations(nodeId);
  const fileUpload = useFileUpload(nodeId, breadcrumbs, content);
  const fileOps = useFileOperations(() => {
    // Reload current folder after file operation
    if (nodeId) {
      void loadNode(nodeId);
    }
  });
  const { previewState, openPreview, closePreview } = useFilePreview();

  // Media lightbox state
  const [lightboxOpen, setLightboxOpen] = React.useState(false);
  const [lightboxIndex, setLightboxIndex] = React.useState(0);

  // Build media items for lightbox (images and videos only)
  const mediaItems = useMemo<MediaItem[]>(() => {
    return sortedFiles
      .filter((file) => isImageFile(file.name) || isVideoFile(file.name))
      .map((file) => {
        const preview = getFilePreview(
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
      navigate(`/files/${parent.id}`);
    } else {
      navigate("/files");
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
    onClick: (folderId: string) => navigate(`/files/${folderId}`),
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

  const isCreatingInThisFolder =
    folderOps.isCreatingFolder && folderOps.newFolderParentId === nodeId;

  if (loading && !content) {
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
        sx={{ position: "relative" }}
      >
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
                  gap: 0.2,
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
                <IconButton
                  color="primary"
                  onClick={fileUpload.handleUploadClick}
                  disabled={!nodeId || loading}
                  title={t("actions.upload")}
                >
                  <UploadFile />
                </IconButton>
                <IconButton
                  color="primary"
                  onClick={folderOps.handleNewFolder}
                  disabled={!nodeId || folderOps.isCreatingFolder}
                  title={t("actions.newFolder")}
                >
                  <CreateNewFolder />
                </IconButton>
                <IconButton
                  onClick={() => navigate("/files")}
                  color="primary"
                  title={t("breadcrumbs.root")}
                >
                  <Home />
                </IconButton>
                {layoutType === InterfaceLayoutType.Tiles ? (
                  <IconButton
                    color="primary"
                    onClick={() => setLayoutType(InterfaceLayoutType.List)}
                    title={t("actions.switchToListView")}
                  >
                    <ViewList />
                  </IconButton>
                ) : (
                  <IconButton
                    color="primary"
                    onClick={() => setLayoutType(InterfaceLayoutType.Tiles)}
                    title={t("actions.switchToTilesView")}
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
                    ns: "files",
                    folders: stats.folders,
                    files: stats.files,
                    size: formatBytes(stats.sizeBytes),
                  })}
                </Typography>
              </Box>
            </Box>

            <FileBreadcrumbs breadcrumbs={breadcrumbs} />

            <Divider
              orientation="vertical"
              flexItem
              sx={{ mx: 1, display: { xs: "none", sm: "block" } }}
            />
            <Box
              sx={{
                flexShrink: 0,
                whiteSpace: "nowrap",
                display: { xs: "none", sm: "block" },
              }}
            >
              <Typography color="text.secondary" sx={{ fontSize: "0.875rem" }}>
                {t("stats.summary", {
                  ns: "files",
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
            />
          )}
        </Box>
      </Box>

      {previewState.isOpen && previewState.fileId && previewState.fileName && (
        <PreviewModal open={previewState.isOpen} onClose={closePreview}>
          {previewState.fileType === "pdf" && (
            <PdfPreview
              fileId={previewState.fileId}
              fileName={previewState.fileName}
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
    </>
  );
};
