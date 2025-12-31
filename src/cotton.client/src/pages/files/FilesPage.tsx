import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Breadcrumbs,
  Link as MuiLink,
  Typography,
  TextField,
  Button,
} from "@mui/material";
import { Add, Folder, InsertDriveFile } from "@mui/icons-material";
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
    return (
      [
        ...sortedFolders.map((node) => ({ kind: "folder", node }) as FolderTile),
        ...sortedFiles.map((file) => ({ kind: "file", file }) as FileTile),
      ]
    );
  }, [sortedFolders, sortedFiles]);

  const canCreateFolder = Boolean(nodeId) && !loading;

  if (loading && !content) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

  const handleCreateFolder = async () => {
    if (!nodeId) return;
    const created = await createFolder(nodeId, newFolderName);
    if (created) {
      setNewFolderName("");
    }
  };

  return (
    <Box p={3} width="100%">
      <Box mb={2}>
        <Typography variant="h4">{t("title")}</Typography>
        <Breadcrumbs aria-label={t("breadcrumbs.ariaLabel")}> 
          <MuiLink component={RouterLink} underline="hover" color="inherit" to="/files">
            {t("breadcrumbs.root")}
          </MuiLink>
          {breadcrumbs.map((crumb, idx) => {
            const isLast = idx === breadcrumbs.length - 1;
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
              >
                {crumb.name}
              </MuiLink>
            );
          })}
        </Breadcrumbs>
      </Box>

      <Box
        mb={2}
        display="flex"
        gap={1}
        flexWrap="wrap"
        alignItems="center"
      >
        <TextField
          size="small"
          label={t("actions.newFolderLabel")}
          placeholder={t("actions.newFolderPlaceholder")}
          value={newFolderName}
          onChange={(e) => setNewFolderName(e.target.value)}
          disabled={!canCreateFolder}
        />
        <Button
          variant="contained"
          startIcon={<Add />}
          onClick={() => void handleCreateFolder()}
          disabled={!canCreateFolder || newFolderName.trim().length === 0}
        >
          {t("actions.createFolder")}
        </Button>
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
              md: "repeat(5, minmax(0, 1fr))",
              lg: "repeat(7, minmax(0, 1fr))",
            },
          }}
        >
          {tiles.map((tile) => {
            if (tile.kind === "folder") {
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
                    p: 1.5,
                    cursor: "pointer",
                    userSelect: "none",
                    outline: "none",
                    "&:hover": { bgcolor: "action.hover" },
                    "&:focus-visible": {
                      boxShadow: (theme) => `0 0 0 2px ${theme.palette.primary.main}`,
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
                      mb: 1,
                    }}
                  >
                    <Folder sx={{ fontSize: 56 }} />
                  </Box>
                  <Typography variant="body2" noWrap title={tile.node.name}>
                    {tile.node.name}
                  </Typography>
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
                  p: 1.5,
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
                  <InsertDriveFile sx={{ fontSize: 56 }} />
                </Box>
                <Typography variant="body2" noWrap title={tile.file.name}>
                  {tile.file.name}
                </Typography>
                <Typography variant="caption" color="text.secondary" noWrap>
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
