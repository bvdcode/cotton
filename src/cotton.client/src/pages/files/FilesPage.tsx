import { useEffect, useMemo, useState } from "react";
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
  Folder,
  Home,
  UploadFile,
} from "@mui/icons-material";
import { Link as RouterLink, useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { PhotoView } from "react-photo-view";
import Loader from "../../shared/ui/Loader";
import { useNodesStore } from "../../shared/store/nodesStore";
import type { NodeDto } from "../../shared/api/layoutsApi";
import { nodesApi, type NodeFileManifestDto } from "../../shared/api/nodesApi";
import { uploadManager } from "../../shared/upload/UploadManager";
import { filesApi } from "../../shared/api/filesApi";
import { FileSystemItemCard } from "./components/FileSystemItemCard";
import { resolveUploadConflicts } from "./utils/uploadConflicts";
import { getFilePreview } from "./utils/getFilePreview";

const formatBytes = (bytes: number): string => {
  if (!Number.isFinite(bytes) || bytes <= 0) return "0 B";
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = bytes;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }
  const precision = unitIndex === 0 ? 0 : value < 10 ? 2 : 1;
  return `${value.toFixed(precision)} ${units[unitIndex]}`;
};

const isImageFile = (fileName: string): boolean => {
  const extension = fileName.toLowerCase().split(".").pop() || "";
  const imageExtensions = ["jpg", "jpeg", "png", "gif", "webp", "bmp", "svg"];
  return imageExtensions.includes(extension);
};

export const FilesPage: React.FC = () => {
  const { t } = useTranslation(["files", "common"]);
  const confirm = useConfirm();
  const navigate = useNavigate();
  const params = useParams<{ nodeId?: string }>();
  const [isCreatingFolder, setIsCreatingFolder] = useState(false);
  const [newFolderName, setNewFolderName] = useState("");
  const [newFolderParentId, setNewFolderParentId] = useState<string | null>(
    null,
  );
  const [isDragging, setIsDragging] = useState(false);

  const {
    currentNode,
    ancestors,
    contentByNodeId,
    loading,
    error,
    loadRoot,
    loadNode,
    createFolder,
  } = useNodesStore();

  const routeNodeId = params.nodeId;

  useEffect(() => {
    if (!routeNodeId) {
      void loadRoot({ force: false });
      return;
    }

    // Store shows cached content immediately (if available), and refetches.
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

  const handleNewFolder = () => {
    // Lock destination folder at the moment user starts creation.
    setNewFolderParentId(nodeId);
    setIsCreatingFolder(true);
    setNewFolderName("");
  };

  const handleConfirmNewFolder = async () => {
    const parentId = newFolderParentId;
    if (!parentId || newFolderName.trim().length === 0) {
      setIsCreatingFolder(false);
      setNewFolderName("");
      setNewFolderParentId(null);
      return;
    }
    await createFolder(parentId, newFolderName.trim());
    setIsCreatingFolder(false);
    setNewFolderName("");
    setNewFolderParentId(null);
  };

  const handleCancelNewFolder = () => {
    setIsCreatingFolder(false);
    setNewFolderName("");
    setNewFolderParentId(null);
  };

  const handleGoUp = () => {
    if (ancestors.length > 0) {
      const parent = ancestors[ancestors.length - 1];
      navigate(`/files/${parent.id}`);
    } else {
      navigate("/files");
    }
  };

  const handleUploadFiles = useMemo(
    () => async (files: FileList | File[]) => {
      if (!nodeId) return;

      // Lock destination folder at the moment user adds files.
      const label = breadcrumbs
        .filter((c, idx) => idx > 0 || c.name !== "Default")
        .map((c) => c.name)
        .join(" / ")
        .trim();

      const list = Array.isArray(files) ? files : Array.from(files);
      if (list.length === 0) return;

      // Prefer cached content; if missing (e.g. first load), fetch names once.
      const contentForCheck = content ?? (await nodesApi.getChildren(nodeId));

      const confirmRename = async (newName: string): Promise<{ confirmed: boolean }> => {
        try {
          await confirm({
            title: t("conflicts.title", { ns: "files" }),
            description: t("conflicts.description", { ns: "files", newName }),
            confirmationText: t("common:actions.confirm"),
            cancellationText: t("common:actions.cancel"),
          });
          return { confirmed: true };
        } catch {
          return { confirmed: false };
        }
      };

      const resolved = await resolveUploadConflicts(list, contentForCheck, confirmRename);

      if (resolved.length === 0) return;

      uploadManager.enqueue(resolved, nodeId, label.length > 0 ? label : t("breadcrumbs.root", { ns: "files" }));
    },
    [nodeId, breadcrumbs, content, confirm, t],
  );

  const handleUploadClick = () => {
    if (!nodeId) return;
    const input = document.createElement('input');
    input.type = 'file';
    input.multiple = true;
    input.onchange = (e) => {
      const files = (e.target as HTMLInputElement).files;
      if (files && files.length > 0) {
        void handleUploadFiles(Array.from(files));
      }
    };
    input.click();
  };

  const handleDownloadFile = async (nodeFileId: string) => {
    try {
      const downloadLink = await filesApi.getDownloadLink(nodeFileId);
      window.open(downloadLink, '_blank');
    } catch (error) {
      console.error('Failed to download file:', error);
    }
  };

  // Recursively collect all files from a directory tree
  const getAllFilesFromItems = async (items: DataTransferItemList): Promise<File[]> => {
    const files: File[] = [];

    const traverseEntry = async (entry: FileSystemEntry): Promise<void> => {
      if (entry.isFile) {
        const fileEntry = entry as FileSystemFileEntry;
        const file = await new Promise<File>((resolve, reject) => {
          fileEntry.file(resolve, reject);
        });
        // Clone the File object to avoid "file could not be read" errors with large files
        const clonedFile = new File([file], file.name, {
          type: file.type,
          lastModified: file.lastModified,
        });
        files.push(clonedFile);
      } else if (entry.isDirectory) {
        const dirEntry = entry as FileSystemDirectoryEntry;
        const reader = dirEntry.createReader();
        
        // Read all entries (might need multiple calls for large directories)
        const readAllEntries = async (): Promise<FileSystemEntry[]> => {
          const allEntries: FileSystemEntry[] = [];
          let batch: FileSystemEntry[] = [];
          
          do {
            batch = await new Promise<FileSystemEntry[]>((resolve, reject) => {
              reader.readEntries(resolve, reject);
            });
            allEntries.push(...batch);
          } while (batch.length > 0);
          
          return allEntries;
        };
        
        const entries = await readAllEntries();
        for (const childEntry of entries) {
          await traverseEntry(childEntry);
        }
      }
    };

    // Process all items in parallel for better performance
    const promises: Promise<void>[] = [];
    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (item.kind === 'file') {
        const entry = item.webkitGetAsEntry();
        if (entry) {
          promises.push(traverseEntry(entry));
        }
      }
    }
    await Promise.all(promises);

    return files;
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (!isDragging) {
      setIsDragging(true);
    }
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    // Only set to false if leaving the main container
    if (e.currentTarget === e.target) {
      setIsDragging(false);
    }
  };

  const handleDrop = async (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);

    if (!nodeId) return;

    // Try to get files from DataTransferItemList (supports directories)
    if (e.dataTransfer.items && e.dataTransfer.items.length > 0) {
      const files = await getAllFilesFromItems(e.dataTransfer.items);
      if (files.length > 0) {
        void handleUploadFiles(files);
      }
    } else if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      // Fallback to simple file list
      void handleUploadFiles(Array.from(e.dataTransfer.files));
    }
  };

  useEffect(() => {
    // If user navigated while the inline-create is open, cancel it
    // so it doesn't "move" to another folder.
    if (!isCreatingFolder) return;
    if (!newFolderParentId) return;
    if (nodeId && newFolderParentId === nodeId) return;

    const timeout = setTimeout(() => {
      setIsCreatingFolder(false);
      setNewFolderName("");
      setNewFolderParentId(null);
    }, 0);

    return () => clearTimeout(timeout);
  }, [isCreatingFolder, newFolderParentId, nodeId]);

  const isCreatingInThisFolder =
    isCreatingFolder &&
    !!newFolderParentId &&
    !!nodeId &&
    newFolderParentId === nodeId;

  if (loading && !content) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

  return (
    <>
      {isDragging && (
        <Box
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          sx={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            bgcolor: 'primary.main',
            opacity: 0.15,
            border: '4px dashed',
            borderColor: 'primary.main',
            zIndex: 9999,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Typography
            variant="h3"
            sx={{
              color: 'primary.main',
              fontWeight: 'bold',
              textShadow: '0 0 10px rgba(255,255,255,0.8)',
              pointerEvents: 'none',
            }}
          >
            Drop files here
          </Typography>
        </Box>
      )}
      <Box 
        p={3} 
        width="100%"
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        sx={{
          position: 'relative',
        }}
      >
      <Box
        mb={2}
        sx={{
          display: "flex",
          gap: 1,
          alignItems: "center",
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
          onClick={handleUploadClick}
          disabled={!nodeId || loading}
          title={t("actions.upload")}
        >
          <UploadFile />
        </IconButton>
        <IconButton
          color="primary"
          onClick={handleNewFolder}
          disabled={!nodeId || isCreatingFolder}
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

        <Breadcrumbs
          aria-label={t("breadcrumbs.ariaLabel")}
          sx={{ ml: 1 }}
        >
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
                  p: {
                    xs: 1,
                    sm: 1.25,
                    md: 1,
                  },
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
                  value={newFolderName}
                  onChange={(e) => setNewFolderName(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      void handleConfirmNewFolder();
                    } else if (e.key === "Escape") {
                      handleCancelNewFolder();
                    }
                  }}
                  onBlur={handleConfirmNewFolder}
                  placeholder={t("actions.folderNamePlaceholder")}
                  slotProps={{
                    input: {
                      sx: {
                        fontSize: { xs: "0.8rem", md: "0.85rem" },
                      },
                    },
                  }}
                />
              </Box>
            )}
            {tiles.map((tile) => {
              if (tile.kind === "folder") {
                return (
                  <FileSystemItemCard
                    key={tile.node.id}
                    icon={<Folder fontSize="large" />}
                    title={tile.node.name}
                    onClick={() => navigate(`/files/${tile.node.id}`)}
                    subtitle={new Date(
                      tile.node.createdAt,
                    ).toLocaleDateString()}
                  />
                );
              }

              const isImage = isImageFile(tile.file.name);
              const preview = getFilePreview(
                tile.file.encryptedFilePreviewHashHex ?? null,
                tile.file.name
              );
              const previewUrl = typeof preview === 'string' ? preview : null;

              return (
                <FileSystemItemCard
                  key={tile.file.id}
                  icon={
                    (() => {
                      if (previewUrl && isImage) {
                        return (
                          <PhotoView
                            src={`/api/v1/files/${tile.file.id}/download`}
                          >
                            <Box
                              component="img"
                              src={previewUrl}
                              alt={tile.file.name}
                              sx={{
                                width: '100%',
                                height: '100%',
                                objectFit: 'cover',
                                cursor: 'pointer',
                              }}
                            />
                          </PhotoView>
                        );
                      }
                      if (previewUrl) {
                        return (
                          <Box
                            component="img"
                            src={previewUrl}
                            alt={tile.file.name}
                            sx={{
                              width: '100%',
                              height: '100%',
                              objectFit: 'cover',
                            }}
                          />
                        );
                      }
                      return preview;
                    })()
                  }
                  title={tile.file.name}
                  subtitle={formatBytes(tile.file.sizeBytes)}
                  onClick={isImage ? undefined : () => handleDownloadFile(tile.file.id)}
                />
              );
            })}
          </Box>
        )}
      </Box>
    </Box>
    </>
  );
};
