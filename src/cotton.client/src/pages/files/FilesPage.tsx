import { useEffect, useMemo } from "react";
import {
  Alert,
  Box,
  Breadcrumbs,
  Link as MuiLink,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Typography,
  Divider,
} from "@mui/material";
import { Folder, InsertDriveFile } from "@mui/icons-material";
import { Link as RouterLink, useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import Loader from "../../shared/ui/Loader";
import { useNodesStore } from "../../shared/store/nodesStore";

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

  if (loading && !content) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

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

      {error && (
        <Box mb={2}>
          <Alert severity="error">{error}</Alert>
        </Box>
      )}

      <Box mb={2}>
        <Typography variant="overline" color="text.secondary">
          {t("sections.folders")}
        </Typography>
        <List dense disablePadding>
          {(content?.nodes ?? []).length === 0 ? (
            <ListItem>
              <ListItemText primary={t("empty.folders")} />
            </ListItem>
          ) : (
            (content?.nodes ?? []).map((node) => (
              <ListItem key={node.id} disablePadding>
                <ListItemButton onClick={() => navigate(`/files/${node.id}`)}>
                  <ListItemIcon>
                    <Folder />
                  </ListItemIcon>
                  <ListItemText primary={node.name} />
                </ListItemButton>
              </ListItem>
            ))
          )}
        </List>
      </Box>

      <Divider />

      <Box mt={2}>
        <Typography variant="overline" color="text.secondary">
          {t("sections.files")}
        </Typography>
        <List dense disablePadding>
          {(content?.files ?? []).length === 0 ? (
            <ListItem>
              <ListItemText primary={t("empty.files")} />
            </ListItem>
          ) : (
            (content?.files ?? []).map((file) => (
              <ListItem key={file.id}>
                <ListItemIcon>
                  <InsertDriveFile />
                </ListItemIcon>
                <ListItemText
                  primary={file.name}
                  secondary={`${formatBytes(file.sizeBytes)} • ${file.contentType}`}
                />
              </ListItem>
            ))
          )}
        </List>
      </Box>

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
