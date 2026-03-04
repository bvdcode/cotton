import * as React from "react";
import {
  Alert,
  Box,
  Breadcrumbs,
  IconButton,
  Link,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Typography,
} from "@mui/material";
import { Download, Folder, InsertDriveFile } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { formatBytes } from "../../../shared/utils/formatBytes";
import { sharedFoldersApi } from "../../../shared/api/sharedFoldersApi";
import type { NodeContentDto } from "../../../shared/api/nodesApi";
import type { Guid } from "../../../shared/api/layoutsApi";

interface BreadcrumbNode {
  id: Guid;
  name: string;
}

interface SharedFolderViewerProps {
  token: string;
  rootNodeId: Guid;
  rootName: string;
}

export const SharedFolderViewer: React.FC<SharedFolderViewerProps> = ({
  token,
  rootNodeId,
  rootName,
}) => {
  const { t } = useTranslation(["share", "common"]);
  const [breadcrumbs, setBreadcrumbs] = React.useState<BreadcrumbNode[]>([]);
  const [content, setContent] = React.useState<NodeContentDto | null>(null);
  const [loading, setLoading] = React.useState<boolean>(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  React.useEffect(() => {
    setBreadcrumbs([{ id: rootNodeId, name: rootName }]);
  }, [rootNodeId, rootName]);

  const currentNode = React.useMemo(
    () => breadcrumbs[breadcrumbs.length - 1] ?? null,
    [breadcrumbs],
  );

  React.useEffect(() => {
    if (!currentNode) return;

    let cancelled = false;

    setLoading(true);
    setLoadError(null);

    void (async () => {
      try {
        const response = await sharedFoldersApi.getChildren(token, {
          nodeId: currentNode.id,
          page: 1,
          pageSize: 1000,
        });

        if (cancelled) return;
        setContent(response.content);
      } catch {
        if (cancelled) return;
        setContent(null);
        setLoadError(t("errors.loadFailed", { ns: "share" }));
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [currentNode, t, token]);

  const handleOpenFolder = React.useCallback((folderId: Guid, folderName: string) => {
    setBreadcrumbs((prev) => [...prev, { id: folderId, name: folderName }]);
  }, []);

  const handleNavigateBreadcrumb = React.useCallback((index: number) => {
    setBreadcrumbs((prev) => prev.slice(0, index + 1));
  }, []);

  const openInlineFile = React.useCallback(
    (nodeFileId: Guid) => {
      const inlineUrl = sharedFoldersApi.buildFileContentUrl(token, nodeFileId, "inline");
      window.open(inlineUrl, "_blank", "noopener,noreferrer");
    },
    [token],
  );

  if (loading) {
    return (
      <Box flex={1} minHeight={0} display="flex" alignItems="center" justifyContent="center">
        <Typography color="text.secondary">{t("loading", { ns: "share" })}</Typography>
      </Box>
    );
  }

  if (loadError) {
    return (
      <Box flex={1} minHeight={0} display="flex" alignItems="center" justifyContent="center" p={2}>
        <Alert severity="error">{loadError}</Alert>
      </Box>
    );
  }

  const folders = content?.nodes ?? [];
  const files = content?.files ?? [];

  return (
    <Box flex={1} minHeight={0} overflow="auto" px={{ xs: 2, sm: 3 }} py={2}>
      <Breadcrumbs sx={{ mb: 1.5 }}>
        {breadcrumbs.map((item, index) => {
          const isLast = index === breadcrumbs.length - 1;
          if (isLast) {
            return (
              <Typography key={item.id} color="text.primary" noWrap>
                {item.name}
              </Typography>
            );
          }

          return (
            <Link
              key={item.id}
              component="button"
              type="button"
              underline="hover"
              color="inherit"
              onClick={() => handleNavigateBreadcrumb(index)}
            >
              {item.name}
            </Link>
          );
        })}
      </Breadcrumbs>

      {folders.length === 0 && files.length === 0 ? (
        <Typography color="text.secondary">{t("folder.empty", { ns: "share" })}</Typography>
      ) : (
        <List disablePadding>
          {folders.map((folder) => (
            <ListItem key={folder.id} disablePadding>
              <ListItemButton onClick={() => handleOpenFolder(folder.id, folder.name)}>
                <ListItemIcon>
                  <Folder color="primary" />
                </ListItemIcon>
                <ListItemText primary={folder.name} secondary={t("folder.kind", { ns: "share" })} />
              </ListItemButton>
            </ListItem>
          ))}

          {files.map((file) => {
            const downloadUrl = sharedFoldersApi.buildFileContentUrl(
              token,
              file.id,
              "download",
            );

            return (
              <ListItem
                key={file.id}
                disablePadding
                secondaryAction={
                  <IconButton
                    edge="end"
                    component="a"
                    href={downloadUrl}
                    title={t("actions.download", { ns: "common" })}
                  >
                    <Download />
                  </IconButton>
                }
              >
                <ListItemButton onClick={() => openInlineFile(file.id)}>
                  <ListItemIcon>
                    <InsertDriveFile />
                  </ListItemIcon>
                  <ListItemText
                    primary={file.name}
                    secondary={formatBytes(file.sizeBytes)}
                  />
                </ListItemButton>
              </ListItem>
            );
          })}
        </List>
      )}
    </Box>
  );
};
