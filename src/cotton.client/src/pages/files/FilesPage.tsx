import { useEffect, useMemo } from "react";
import {
  Alert,
  Box,
  Breadcrumbs,
  IconButton,
  Link as MuiLink,
  TextField,
  Typography,
} from "@mui/material";
import {
  ArrowUpward,
  CreateNewFolder,
  Delete,
  Edit,
  Folder,
  Home,
  UploadFile,
} from "@mui/icons-material";
import { Link as RouterLink, useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import Loader from "../../shared/ui/Loader";
import { useNodesStore } from "../../shared/store/nodesStore";
import type { NodeDto } from "../../shared/api/layoutsApi";
import type { NodeFileManifestDto } from "../../shared/api/nodesApi";
import { filesApi } from "../../shared/api/filesApi";
import { FileSystemItemCard } from "./components/FileSystemItemCard";
import { ImagePreviewIcon } from "./components/ImagePreviewIcon";
import { ImageLoaderProvider } from "./components/ImageLoaderProvider";
import { FolderCard } from "./components/FolderCard";
import { getFilePreview } from "./utils/getFilePreview";
import { formatBytes } from "./utils/formatBytes";
import { isImageFile } from "./utils/isImageFile";
import { useFolderOperations } from "./hooks/useFolderOperations";
import { useFileUpload } from "./hooks/useFileUpload";
import { useFileOperations } from "./hooks/useFileOperations";

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

  useEffect(() => {
    if (!folderOps.isCreatingFolder) return;
    if (!folderOps.newFolderParentId) return;
    if (nodeId && folderOps.newFolderParentId === nodeId) return;

    const timeout = setTimeout(() => {
      folderOps.handleCancelNewFolder();
    }, 0);

    return () => clearTimeout(timeout);
  }, [folderOps, nodeId]);

  const isCreatingInThisFolder =
    folderOps.isCreatingFolder &&
    !!folderOps.newFolderParentId &&
    !!nodeId &&
    folderOps.newFolderParentId === nodeId;

  if (loading && !content) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

  return (
    <ImageLoaderProvider>
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
              Drop files here
            </Typography>
          </Box>
        )}
      <Box
        p={3}
        width="100%"
        onDragOver={fileUpload.handleDragOver}
        onDragLeave={fileUpload.handleDragLeave}
        onDrop={fileUpload.handleDrop}
        sx={{ position: "relative" }}
      >
        <Box mb={2}>
            {/* Breadcrumbs на мобилке над кнопками */}
            <Box
              sx={{
                display: { xs: "flex", sm: "none" },
                mb: 1,
                overflow: "auto",
                "&::-webkit-scrollbar": { display: "none" },
                msOverflowStyle: "none",
                scrollbarWidth: "none",
              }}
              ref={(el: HTMLDivElement | null) => {
                if (el) el.scrollLeft = el.scrollWidth;
              }}
            >
              <Breadcrumbs aria-label={t("breadcrumbs.ariaLabel")} sx={{ whiteSpace: "nowrap" }}>
                {breadcrumbs
                  .filter((crumb, idx) => idx > 0 || crumb.name !== "Default")
                  .map((crumb, idx, filtered) => {
                    const isLast = idx === filtered.length - 1;
                    if (isLast) {
                      return (
                        <Typography key={crumb.id} color="text.primary">
                          {crumb.name}
                        </Typography>
                      );
                    }
                    return (
                      <MuiLink
                        key={crumb.id}
                        component={RouterLink}
                        underline="hover"
                        color="inherit"
                        to={`/files/${crumb.id}`}
                        sx={{ fontSize: "1.1rem" }}
                      >
                        {crumb.name}
                      </MuiLink>
                    );
                  })}
              </Breadcrumbs>
            </Box>

            {/* Кнопки и breadcrumbs на больших экранах */}
            <Box sx={{ display: "flex", gap: 1, alignItems: "center" }}>
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

              {/* Breadcrumbs на больших экранах */}
              <Box
                sx={{
                  display: { xs: "none", sm: "flex" },
                  ml: 1,
                  overflow: "auto",
                  flex: 1,
                  "&::-webkit-scrollbar": { display: "none" },
                  msOverflowStyle: "none",
                  scrollbarWidth: "none",
                }}
                ref={(el: HTMLDivElement | null) => {
                  if (el) el.scrollLeft = el.scrollWidth;
                }}
              >
                <Breadcrumbs aria-label={t("breadcrumbs.ariaLabel")} sx={{ whiteSpace: "nowrap" }}>
                  {breadcrumbs
                .filter((crumb, idx) => idx > 0 || crumb.name !== "Default")
                .map((crumb, idx, filtered) => {
                  const isLast = idx === filtered.length - 1;
                  if (isLast) {
                    return (
                      <Typography key={crumb.id} color="text.primary">
                        {crumb.name}
                      </Typography>
                    );
                  }
                  return (
                    <MuiLink
                      key={crumb.id}
                      component={RouterLink}
                      underline="hover"
                      color="inherit"
                      to={`/files/${crumb.id}`}
                      sx={{ fontSize: "1.1rem" }}
                    >
                      {crumb.name}
                    </MuiLink>
                  );
                })}
                </Breadcrumbs>
              </Box>
            </Box>
          </Box>

          {error && (
            <Box mb={2}>
              <Alert severity="error">{error}</Alert>
            </Box>
          )}

          <Box>
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
                        "& > svg": { width: "70%", height: "70%" },
                      }}
                    >
                      <Folder sx={{ color: "primary.main" }} />
                    </Box>
                    <TextField
                      autoFocus
                      fullWidth
                      size="small"
                      value={folderOps.newFolderName}
                      onChange={(e) => folderOps.setNewFolderName(e.target.value)}
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
                          folderOps.handleRenameFolder(tile.node.id, tile.node.name)
                        }
                        onDelete={() =>
                          folderOps.handleDeleteFolder(tile.node.id, tile.node.name)
                        }
                        onClick={() => navigate(`/files/${tile.node.id}`)}
                      />
                    );
                  }

                  const isImage = isImageFile(tile.file.name);
                  const preview = getFilePreview(
                    tile.file.encryptedFilePreviewHashHex ?? null,
                    tile.file.name,
                  );
                  const previewUrl = typeof preview === "string" ? preview : null;

                  return (
                    <FileSystemItemCard
                      key={tile.file.id}
                      icon={(() => {
                        if (previewUrl && isImage) {
                          return (
                            <ImagePreviewIcon
                              nodeFileId={tile.file.id}
                              fileName={tile.file.name}
                              previewUrl={previewUrl}
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
                        isImage && previewUrl
                          ? undefined
                          : () => handleDownloadFile(tile.file.id, tile.file.name)
                      }
                      actions={[
                        {
                          icon: <Edit fontSize="small" />,
                          onClick: () => fileOps.handleRenameFile(tile.file.id, tile.file.name),
                          tooltip: t("common:actions.rename"),
                        },
                        {
                          icon: <Delete fontSize="small" />,
                          onClick: () => fileOps.handleDeleteFile(tile.file.id, tile.file.name),
                          tooltip: t("common:actions.delete"),
                        },
                      ]}
                    />
                  );
                })}
              </Box>
            )}
        </Box>
      </Box>
    </ImageLoaderProvider>
  );
};
