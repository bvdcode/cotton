import React, { useEffect, useMemo } from "react";
import {
  Alert,
  Box,
  Divider,
  IconButton,
  TextField,
  Typography,
} from "@mui/material";
import { FileBreadcrumbs } from "./components";
import {
  ArrowUpward,
  CreateNewFolder,
  Delete,
  Download,
  Edit,
  Folder,
  Home,
  UploadFile,
} from "@mui/icons-material";
import { useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import Loader from "../../shared/ui/Loader";
import { useNodesStore } from "../../shared/store/nodesStore";
import type { NodeDto } from "../../shared/api/layoutsApi";
import type { NodeFileManifestDto } from "../../shared/api/nodesApi";
import { filesApi } from "../../shared/api/filesApi";
import { PhotoProvider, PhotoView } from "react-photo-view";
import { FolderCard } from "./components/FolderCard";
import { RenamableItemCard } from "./components/RenamableItemCard";
import { getFilePreview } from "./utils/getFilePreview";
import { formatBytes } from "./utils/formatBytes";
import { isImageFile, isVideoFile } from "./utils/fileTypes";
import {
  PreviewModal,
  PdfPreview,
  renderVideoPreview,
  renderLazyImage,
  VIDEO_WIDTH,
  VIDEO_HEIGHT,
} from "./components/preview";
import { useFolderOperations } from "./hooks/useFolderOperations";
import { useFileUpload } from "./hooks/useFileUpload";
import { useFileOperations } from "./hooks/useFileOperations";
import { useFilePreview } from "./hooks/useFilePreview";

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

  const breadcrumbs = useMemo(() => {
    if (!currentNode) return [] as Array<{ id: string; name: string }>;
    const chain = [...ancestors, currentNode];
    return chain.map((n) => ({ id: n.id, name: n.name }));
  }, [ancestors, currentNode]);

  const sortedFolders = useMemo(() => {
    const nodes = (content?.nodes ?? []).slice();
    nodes.sort((a, b) => a.name.localeCompare(b.name));
    return nodes;
  }, [content?.nodes]);

  const sortedFiles = useMemo(() => {
    const files = (content?.files ?? []).slice();
    files.sort((a, b) => a.name.localeCompare(b.name));
    return files;
  }, [content?.files]);

  const tiles = useMemo(() => {
    type FolderTile = { kind: "folder"; node: NodeDto };
    type FileTile = { kind: "file"; file: NodeFileManifestDto };
    return [
      ...sortedFolders.map((node) => ({ kind: "folder", node } as FolderTile)),
      ...sortedFiles.map((file) => ({ kind: "file", file } as FileTile)),
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

  const isCreatingInThisFolder =
    folderOps.isCreatingFolder && folderOps.newFolderParentId === nodeId;

  if (loading && !content) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

  return (
    <PhotoProvider
      maskOpacity={0.95}
      bannerVisible={true}
      photoClosable={true}
      maskClosable={true}
      pullClosable={true}
    >
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
              display: "flex",
              flexDirection: "column",
              marginTop: 1,
              marginBottom: 5,
              borderBottom: 1,
              borderColor: "divider",
              zIndex: 10,
            }}
          >
            <Box
              sx={{
                position: "sticky",
                top: 0,
                zIndex: 20,
                bgcolor: "background.default",
                display: "flex",
                gap: 1,
                alignItems: "center",
                marginBottom: 1,
              }}
            >
              <Box
                sx={{
                  display: "flex",
                  flexDirection: { xs: "column", sm: "row" },
                  gap: 1,
                  alignItems: { xs: "stretch", sm: "center" },
                  minWidth: 0,
                  flex: "1 1 auto",
                  overflow: "hidden",
                }}
              >
                <Box
                  sx={{
                    display: "flex",
                    gap: 1,
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
                </Box>

                <FileBreadcrumbs breadcrumbs={breadcrumbs} />
              </Box>
              <Divider orientation="vertical" flexItem sx={{ mx: 1 }} />
              <Box sx={{ flexShrink: 0, whiteSpace: "nowrap" }}>
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
          </Box>
          {error && (
            <Box mb={1} px={1}>
              <Alert severity="error">{error}</Alert>
            </Box>
          )}

          <Box px={3} pb={3}>
            {tiles.length === 0 && !isCreatingInThisFolder ? (
              <Typography color="text.secondary">{t("empty.all")}</Typography>
            ) : (
              <Box
                sx={{
                  display: "grid",
                  gap: 1.5,
                  gridTemplateColumns: {
                    xs: "repeat(3, minmax(0, 1fr))",
                    sm: "repeat(4, minmax(0, 1fr))",
                    md: "repeat(6, minmax(0, 1fr))",
                    lg: "repeat(8, minmax(0, 1fr))",
                  },
                }}
              >
                {isCreatingInThisFolder && (
                  <Box
                    sx={{
                      border: "2px solid",
                      borderColor: "primary.main",
                      borderRadius: 2,
                      p: { xs: 1, sm: 1.25, md: 1 },
                      bgcolor: "action.hover",
                    }}
                  >
                    <Box
                      sx={{
                        width: "100%",
                        aspectRatio: "1 / 1",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        borderRadius: 1.5,
                        overflow: "hidden",
                        mb: 0.75,
                        "& > svg": {
                          width: "70%",
                          height: "70%",
                        },
                      }}
                    >
                      <Folder sx={{ color: "primary.main" }} />
                    </Box>
                    <TextField
                      autoFocus
                      fullWidth
                      size="small"
                      value={folderOps.newFolderName}
                      onChange={(e) =>
                        folderOps.setNewFolderName(e.target.value)
                      }
                      onKeyDown={(e) => {
                        if (e.key === "Enter") {
                          void folderOps.handleConfirmNewFolder();
                        } else if (e.key === "Escape") {
                          folderOps.handleCancelNewFolder();
                        }
                      }}
                      onBlur={folderOps.handleConfirmNewFolder}
                      placeholder={t("actions.folderNamePlaceholder")}
                      slotProps={{
                        input: {
                          sx: { fontSize: { xs: "0.8rem", md: "0.85rem" } },
                        },
                      }}
                    />
                  </Box>
                )}

                {tiles.map((tile) => {
                  if (tile.kind === "folder") {
                    return (
                      <FolderCard
                        key={tile.node.id}
                        folder={tile.node}
                        isRenaming={folderOps.renamingFolderId === tile.node.id}
                        renamingName={folderOps.renamingFolderName}
                        onRenamingNameChange={folderOps.setRenamingFolderName}
                        onConfirmRename={folderOps.handleConfirmRename}
                        onCancelRename={folderOps.handleCancelRename}
                        onStartRename={() =>
                          folderOps.handleRenameFolder(
                            tile.node.id,
                            tile.node.name,
                          )
                        }
                        onDelete={() =>
                          folderOps.handleDeleteFolder(
                            tile.node.id,
                            tile.node.name,
                          )
                        }
                        onClick={() => navigate(`/files/${tile.node.id}`)}
                      />
                    );
                  }

                  const isImage = isImageFile(tile.file.name);
                  const isVideo = isVideoFile(tile.file.name);
                  const preview = getFilePreview(
                    tile.file.encryptedFilePreviewHashHex ?? null,
                    tile.file.name,
                  );
                  const previewUrl =
                    typeof preview === "string" ? preview : null;

                  const iconContainerSx = previewUrl
                    ? {
                        mx: { xs: -1, sm: -1.25, md: -1 },
                        mt: { xs: -1, sm: -1.25, md: -1 },
                        width: {
                          xs: "calc(100% + 16px)",
                          sm: "calc(100% + 20px)",
                          md: "calc(100% + 16px)",
                        },
                        borderRadius: 0,
                      }
                    : undefined;

                  // Common card component
                  const fileCard = (
                    <RenamableItemCard
                      icon={(() => {
                        if (previewUrl && (isImage || isVideo)) {
                          return (
                            <Box
                              component="img"
                              src={previewUrl}
                              alt={tile.file.name}
                              loading="lazy"
                              decoding="async"
                              sx={{
                                width: "100%",
                                height: "100%",
                                objectFit: "cover",
                                cursor: isImage || isVideo ? "pointer" : "default",
                              }}
                            />
                          );
                        }
                        if (previewUrl) {
                          return (
                            <Box
                              component="img"
                              src={previewUrl}
                              alt={tile.file.name}
                              loading="lazy"
                              decoding="async"
                              sx={{
                                width: "100%",
                                height: "100%",
                                objectFit: "cover",
                              }}
                            />
                          );
                        }
                        return preview;
                      })()}
                      title={tile.file.name}
                      subtitle={formatBytes(tile.file.sizeBytes)}
                      onClick={
                        isImage || isVideo
                          ? undefined
                          : () => handleFileClick(tile.file.id, tile.file.name)
                      }
                      iconContainerSx={iconContainerSx}
                      actions={[
                        {
                          icon: <Download />,
                          onClick: () =>
                            handleDownloadFile(tile.file.id, tile.file.name),
                          tooltip: t("common:actions.download"),
                        },
                        {
                          icon: <Edit />,
                          onClick: () =>
                            fileOps.handleRenameFile(
                              tile.file.id,
                              tile.file.name,
                            ),
                          tooltip: t("common:actions.rename"),
                        },
                        {
                          icon: <Delete />,
                          onClick: () =>
                            fileOps.handleDeleteFile(
                              tile.file.id,
                              tile.file.name,
                            ),
                          tooltip: t("common:actions.delete"),
                        },
                      ]}
                      isRenaming={fileOps.renamingFileId === tile.file.id}
                      renamingValue={fileOps.renamingFileName}
                      onRenamingValueChange={fileOps.setRenamingFileName}
                      onConfirmRename={() => {
                        void fileOps.handleConfirmRename();
                      }}
                      onCancelRename={fileOps.handleCancelRename}
                      placeholder={t("rename.fileNamePlaceholder", {
                        ns: "files",
                      })}
                    />
                  );

                  // Only wrap images and videos in PhotoView
                  if (isImage || isVideo) {
                    return (
                      <PhotoView
                        key={tile.file.id}
                        width={isVideo ? VIDEO_WIDTH : undefined}
                        height={isVideo ? VIDEO_HEIGHT : undefined}
                        render={
                          isVideo
                            ? renderVideoPreview(tile.file.id, tile.file.name)
                            : renderLazyImage(tile.file.id, tile.file.name)
                        }
                      >
                        {fileCard}
                      </PhotoView>
                    );
                  }

                  // Other files without PhotoView wrapper
                  return (
                    <React.Fragment key={tile.file.id}>
                      {fileCard}
                    </React.Fragment>
                  );
                })}
              </Box>
            )}
          </Box>
        </Box>

        {previewState.isOpen &&
          previewState.fileId &&
          previewState.fileName && (
            <PreviewModal open={previewState.isOpen} onClose={closePreview}>
              {previewState.fileType === "pdf" && (
                <PdfPreview
                  fileId={previewState.fileId}
                  fileName={previewState.fileName}
                />
              )}
            </PreviewModal>
          )}
      </PhotoProvider>
  );
};
