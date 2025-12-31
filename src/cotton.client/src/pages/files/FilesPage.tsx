import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Breadcrumbs,
  IconButton,
  Link as MuiLink,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import {
  ArrowUpward,
  CreateNewFolder,
  Folder,
  Home,
  InsertDriveFile,
  UploadFile,
} from "@mui/icons-material";
import { Link as RouterLink, useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import Loader from "../../shared/ui/Loader";
import { useNodesStore } from "../../shared/store/nodesStore";
import type { NodeDto } from "../../shared/api/layoutsApi";
import type { NodeFileManifestDto } from "../../shared/api/nodesApi";
import { uploadManager } from "../../shared/upload/UploadManager";
import { FileSystemItemCard } from "./components/FileSystemItemCard";

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

export const FilesPage: React.FC = () => {
  const { t } = useTranslation("files");
  const navigate = useNavigate();
  const params = useParams<{ nodeId?: string }>();
  const [isCreatingFolder, setIsCreatingFolder] = useState(false);
  const [newFolderName, setNewFolderName] = useState("");
  const [newFolderParentId, setNewFolderParentId] = useState<string | null>(
    null,
  );

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

  const handleUploadFiles = (files: FileList | File[]) => {
    if (!nodeId) return;
    // Lock destination folder at the moment user adds files.
    const label = breadcrumbs
      .filter((c, idx) => idx > 0 || c.name !== "Default")
      .map((c) => c.name)
      .join(" / ")
      .trim();
    uploadManager.enqueue(
      files,
      nodeId,
      label.length > 0 ? label : t("breadcrumbs.root"),
    );
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
    <Box p={3} width="100%">
      <Box mb={2} display="flex" alignItems="center" gap={2}>
        <Box display="flex" gap={1}>
          <Tooltip title={t("actions.goUp")}>
            <span style={{ display: "inline-flex" }}>
              <IconButton
                color="primary"
                onClick={handleGoUp}
                disabled={loading || ancestors.length === 0}
              >
                <ArrowUpward />
              </IconButton>
            </span>
          </Tooltip>
          <Tooltip title={t("actions.upload")}>
            <span style={{ display: "inline-flex" }}>
              <IconButton
                color="primary"
                onClick={() => {
                  if (!nodeId) return;
                  const label = breadcrumbs
                    .filter((c, idx) => idx > 0 || c.name !== "Default")
                    .map((c) => c.name)
                    .join(" / ")
                    .trim();

                  uploadManager.openFilePicker({
                    nodeId,
                    nodeLabel: label.length > 0 ? label : t("breadcrumbs.root"),
                    multiple: true,
                    accept: "*/*",
                  });
                }}
                disabled={!nodeId || loading}
              >
                <UploadFile />
              </IconButton>
            </span>
          </Tooltip>
          <Tooltip title={t("actions.newFolder")}>
            <span style={{ display: "inline-flex" }}>
              <IconButton
                color="primary"
                onClick={handleNewFolder}
                disabled={!nodeId || loading || isCreatingFolder}
              >
                <CreateNewFolder />
              </IconButton>
            </span>
          </Tooltip>
          <Tooltip title={t("breadcrumbs.root")}>
            <IconButton onClick={() => navigate("/files")} color="primary">
              <Home />
            </IconButton>
          </Tooltip>
        </Box>

        <Breadcrumbs aria-label={t("breadcrumbs.ariaLabel")}>
          {breadcrumbs
            .filter((crumb, idx) => idx > 0 || crumb.name !== "Default")
            .map((crumb, idx, filtered) => {
              const isLast = idx === filtered.length - 1;
              if (isLast) {
                return (
                  <Typography key={crumb.id} color="text.primary" variant="h6">
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

      <Box
        onDragOver={(e) => {
          e.preventDefault();
        }}
        onDrop={(e) => {
          e.preventDefault();
          const files = e.dataTransfer?.files;
          if (files && files.length > 0) {
            handleUploadFiles(files);
          }
        }}
      >
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
                  p: 1.5,
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
                    bgcolor: "background.default",
                    borderRadius: 1.5,
                    mb: 1,
                  }}
                >
                  <Folder sx={{ fontSize: 56, color: "primary.main" }} />
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

              return (
                <FileSystemItemCard
                  key={tile.file.id}
                  icon={<InsertDriveFile sx={{ fontSize: 56 }} />}
                  title={tile.file.name}
                  subtitle={formatBytes(tile.file.sizeBytes)}
                />
              );
            })}
          </Box>
        )}
      </Box>
    </Box>
  );
};
