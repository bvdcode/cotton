import * as React from "react";
import {
  Alert,
  Box,
  Breadcrumbs,
  IconButton,
  Link,
  Typography,
} from "@mui/material";
import { ViewList, ViewModule } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { formatBytes } from "../../../shared/utils/formatBytes";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import { MediaLightbox } from "../../files/components";
import { FileListViewFactory } from "../../files/components/views/FileListViewFactory";
import type {
  FileOperations,
  FolderOperations,
  TilesSize,
} from "../../files/types/FileListViewTypes";
import { getFileIcon } from "../../files/utils/icons";
import { isImageFile, isVideoFile } from "../../files/utils/fileTypes";
import { useContentTiles } from "../../../shared/hooks/useContentTiles";
import { sharedFoldersApi } from "../../../shared/api/sharedFoldersApi";
import type { Guid } from "../../../shared/api/layoutsApi";
import type { SharedNodeContentDto } from "../../../shared/api/sharedFoldersApi";
import type { MediaItem } from "../../files/components";

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
  const [content, setContent] = React.useState<SharedNodeContentDto | null>(null);
  const [loading, setLoading] = React.useState<boolean>(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const [layoutType, setLayoutType] = React.useState<InterfaceLayoutType>(InterfaceLayoutType.Tiles);
  const [tilesSize, setTilesSize] = React.useState<TilesSize>("medium");
  const [lightboxOpen, setLightboxOpen] = React.useState<boolean>(false);
  const [lightboxIndex, setLightboxIndex] = React.useState<number>(0);

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

  const viewMode: "table" | "tiles-small" | "tiles-medium" | "tiles-large" = React.useMemo(() => {
    if (layoutType === InterfaceLayoutType.List) return "table";
    if (tilesSize === "small") return "tiles-small";
    if (tilesSize === "large") return "tiles-large";
    return "tiles-medium";
  }, [layoutType, tilesSize]);

  const cycleViewMode = React.useCallback(() => {
    switch (viewMode) {
      case "table":
        setLayoutType(InterfaceLayoutType.Tiles);
        setTilesSize("small");
        return;
      case "tiles-small":
        setTilesSize("medium");
        return;
      case "tiles-medium":
        setTilesSize("large");
        return;
      case "tiles-large":
        setLayoutType(InterfaceLayoutType.List);
    }
  }, [viewMode]);

  const { sortedFiles, tiles } = useContentTiles(content ?? undefined);

  const mediaItems = React.useMemo<MediaItem[]>(() => {
    return sortedFiles
      .filter((file) => isImageFile(file.name) || isVideoFile(file.name))
      .map((file) => {
        const preview = getFileIcon(
          file.previewHashEncryptedHex ?? null,
          file.name,
          file.contentType,
        );
        const previewUrl = typeof preview === "string" ? preview : "";

        return {
          id: file.id,
          kind: isImageFile(file.name) ? "image" : "video",
          name: file.name,
          previewUrl,
          mimeType: file.contentType,
          sizeBytes: file.sizeBytes,
        };
      });
  }, [sortedFiles]);

  const handleMediaClick = React.useCallback((fileId: string) => {
    const mediaIndex = mediaItems.findIndex((item) => item.id === fileId);
    if (mediaIndex < 0) {
      return;
    }

    setLightboxIndex(mediaIndex);
    setLightboxOpen(true);
  }, [mediaItems]);

  const handleFileClick = React.useCallback(async (fileId: string) => {
    try {
      await sharedFoldersApi.openFileInline(token, fileId);
    } catch {
      // ignore
    }
  }, [token]);

  const handleDownload = React.useCallback(async (fileId: string, fileName: string) => {
    try {
      await sharedFoldersApi.downloadFile(token, fileId, fileName);
    } catch {
      // ignore
    }
  }, [token]);

  const fileOperations = React.useMemo<FileOperations>(() => ({
    isRenaming: () => false,
    getRenamingName: () => "",
    onRenamingNameChange: () => {},
    onConfirmRename: async () => {},
    onCancelRename: () => {},
    onStartRename: () => {},
    onDelete: () => {},
    onDownload: (fileId: string, fileName: string) => {
      void handleDownload(fileId, fileName);
    },
    onShare: () => {},
    onClick: (fileId: string) => {
      void handleFileClick(fileId);
    },
    onMediaClick: handleMediaClick,
  }), [handleDownload, handleFileClick, handleMediaClick]);

  const folderOperations = React.useMemo<FolderOperations>(() => ({
    isRenaming: () => false,
    getRenamingName: () => "",
    onRenamingNameChange: () => {},
    onConfirmRename: () => {},
    onCancelRename: () => {},
    onStartRename: () => {},
    onDelete: () => {},
    onClick: (folderId: string) => {
      const folder = content?.nodes.find((x) => x.id === folderId);
      if (!folder) return;
      handleOpenFolder(folder.id, folder.name);
    },
  }), [content?.nodes, handleOpenFolder]);

  const stats = React.useMemo(() => {
    const foldersCount = content?.nodes.length ?? 0;
    const filesCount = content?.files.length ?? 0;
    const sizeBytes = (content?.files ?? []).reduce((acc, file) => acc + file.sizeBytes, 0);

    return {
      folders: foldersCount,
      files: filesCount,
      sizeBytes,
    };
  }, [content?.files, content?.nodes.length]);

  const getSignedMediaUrl = React.useCallback(async (fileId: string): Promise<string> => {
    return `${sharedFoldersApi.buildFileContentUrl(token, fileId, "inline")}&preview=true`;
  }, [token]);

  const getDownloadUrl = React.useCallback(async (fileId: string): Promise<string> => {
    return sharedFoldersApi.buildFileContentUrl(token, fileId, "download");
  }, [token]);

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

  const nextViewTitleKey: string = (() => {
    switch (viewMode) {
      case "table":
        return "actions.switchToSmallTilesView";
      case "tiles-small":
        return "actions.switchToMediumTilesView";
      case "tiles-medium":
        return "actions.switchToLargeTilesView";
      case "tiles-large":
        return "actions.switchToTableView";
      default:
        return "actions.switchToTableView";
    }
  })();

  const viewIcon =
    viewMode === "table" ? (
      <ViewList />
    ) : (
      <ViewModule
        sx={{
          transform:
            viewMode === "tiles-small"
              ? "scale(0.9)"
              : viewMode === "tiles-large"
                ? "scale(1.1)"
                : "scale(1)",
        }}
      />
    );

  return (
    <Box flex={1} minHeight={0} overflow="auto" px={{ xs: 2, sm: 3 }} py={2}>
      <Box
        display="flex"
        alignItems="center"
        justifyContent="space-between"
        gap={2}
        mb={1.5}
      >
        <Breadcrumbs>
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

        <IconButton color="primary" onClick={cycleViewMode} title={t(nextViewTitleKey, { ns: "files" })}>
          {viewIcon}
        </IconButton>
      </Box>

      <FileListViewFactory
        layoutType={layoutType}
        tiles={tiles}
        folderOperations={folderOperations}
        fileOperations={fileOperations}
        isCreatingFolder={false}
        newFolderName=""
        onNewFolderNameChange={() => {}}
        onConfirmNewFolder={async () => {}}
        onCancelNewFolder={() => {}}
        folderNamePlaceholder={t("actions.folderNamePlaceholder", { ns: "files" })}
        fileNamePlaceholder={t("rename.fileNamePlaceholder", { ns: "files" })}
        emptyStateText={t("folder.empty", { ns: "share" })}
        tileSize={tilesSize}
        loading={loading}
      />

      <MediaLightbox
        items={mediaItems}
        open={lightboxOpen}
        initialIndex={lightboxIndex}
        onClose={() => setLightboxOpen(false)}
        getSignedMediaUrl={getSignedMediaUrl}
        getDownloadUrl={getDownloadUrl}
      />

      <Typography color="text.secondary" sx={{ mt: 1 }}>
        {t("stats.summary", {
          ns: "files",
          folders: stats.folders,
          files: stats.files,
          size: formatBytes(stats.sizeBytes),
        })}
      </Typography>
    </Box>
  );
};
