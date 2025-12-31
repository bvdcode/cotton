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
} from "@mui/icons-material";
import { Link as RouterLink, useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import Loader from "../../shared/ui/Loader";
import { useNodesStore } from "../../shared/store/nodesStore";
import type { NodeDto } from "../../shared/api/layoutsApi";
import type { NodeFileManifestDto } from "../../shared/api/nodesApi";

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

  if (loading && !content) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

  const handleNewFolder = () => {
    setIsCreatingFolder(true);
    setNewFolderName("");
  };

  const handleConfirmNewFolder = async () => {
    if (!nodeId || newFolderName.trim().length === 0) {
      setIsCreatingFolder(false);
      setNewFolderName("");
      return;
    }
    await createFolder(nodeId, newFolderName.trim());
    setIsCreatingFolder(false);
    setNewFolderName("");
  };

  const handleCancelNewFolder = () => {
    setIsCreatingFolder(false);
    setNewFolderName("");
  };

  const handleGoUp = () => {
    if (ancestors.length > 0) {
      const parent = ancestors[ancestors.length - 1];
      navigate(`/files/${parent.id}`);
    } else {
      navigate("/files");
    }
  };

  return (
    <Box p={3} width="100%">
      <Box mb={2} display="flex" alignItems="center" gap={2}>
        <Box display="flex" gap={1}>
          <Tooltip title={t("actions.goUp")}>
            <IconButton
              color="primary"
              onClick={handleGoUp}
              disabled={loading || ancestors.length === 0}
            >
              <ArrowUpward />
            </IconButton>
          </Tooltip>
          <Tooltip title={t("actions.newFolder")}>
            <IconButton
              color="primary"
              onClick={handleNewFolder}
              disabled={!nodeId || loading || isCreatingFolder}
            >
              <CreateNewFolder />
            </IconButton>
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

      {tiles.length === 0 ? (
        <Typography color="text.secondary">{t("empty.all")}</Typography>
      ) : (
        <Box
          sx={{
            display: "grid",
            gap: 2,
            gridTemplateColumns: {
              xs: "repeat(2, minmax(0, 1fr))",
              sm: "repeat(3, minmax(0, 1fr))",
              md: "repeat(4, minmax(0, 1fr))",
              lg: "repeat(5, minmax(0, 1fr))",
            },
          }}
        >
          {isCreatingFolder && (
            <Box
              sx={{
                border: "2px solid",
                borderColor: "primary.main",
                borderRadius: 2,
                p: 2,
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
                  mb: 1.5,
                }}
              >
                <Folder sx={{ fontSize: 80, color: "primary.main" }} />
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
              const createdDate = tile.node.createdAt
                ? new Date(tile.node.createdAtUtc).toLocaleDateString()
                : "";
              return (
                <Box
                  key={tile.node.id}
                  role="button"
                  tabIndex={0}
                  onClick={() => navigate(`/files/${tile.node.id}`)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      navigate(`/files/${tile.node.id}`);
                    }
                  }}
                  sx={{
                    border: "1px solid",
                    borderColor: "divider",
                    borderRadius: 2,
                    p: 2,
                    cursor: "pointer",
                    userSelect: "none",
                    outline: "none",
                    "&:hover": { bgcolor: "action.hover" },
                    "&:focus-visible": {
                      boxShadow: (theme) =>
                        `0 0 0 2px ${theme.palette.primary.main}`,
                    },
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
                      mb: 1.5,
                    }}
                  >
                    <Folder sx={{ fontSize: 80 }} />
                  </Box>
                  <Typography
                    variant="body1"
                    noWrap
                    title={tile.node.name}
                    fontWeight={500}
                  >
                    {tile.node.name}
                  </Typography>
                  {createdDate && (
                    <Typography
                      variant="caption"
                      color="text.secondary"
                      display="block"
                    >
                      {createdDate}
                    </Typography>
                  )}
                </Box>
              );
            }

            return (
              <Box
                key={tile.file.id}
                sx={{
                  border: "1px solid",
                  borderColor: "divider",
                  borderRadius: 2,
                  p: 2,
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
                    mb: 1.5,
                  }}
                >
                  <InsertDriveFile sx={{ fontSize: 80 }} />
                </Box>
                <Typography
                  variant="body1"
                  noWrap
                  title={tile.file.name}
                  fontWeight={500}
                >
                  {tile.file.name}
                </Typography>
                <Typography
                  variant="caption"
                  color="text.secondary"
                  display="block"
                >
                  {formatBytes(tile.file.sizeBytes)}
                </Typography>
              </Box>
            );
          })}
        </Box>
      )}

      {loading && content && (
        <Box mt={2}>
          <Typography variant="caption" color="text.secondary">
            {t("refreshing")}
          </Typography>
        </Box>
      )}
    </Box>
  );
};
