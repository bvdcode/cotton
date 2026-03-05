import * as React from "react";
import { Alert, Box, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import { MediaLightbox, PageHeader } from "../../files/components";
import { FileListViewFactory } from "../../files/components/views/FileListViewFactory";
import type {
  FileOperations,
  FolderOperations,
  TilesSize,
} from "../../files/types/FileListViewTypes";
import { getFileIcon } from "../../files/utils/icons";
import { getFileTypeInfo } from "../../files/utils/fileTypes";
import { useContentTiles } from "../../../shared/hooks/useContentTiles";
import { sharedFoldersApi } from "../../../shared/api/sharedFoldersApi";
import type { Guid } from "../../../shared/api/layoutsApi";
import type { SharedNodeContentDto } from "../../../shared/api/sharedFoldersApi";
import type { MediaItem } from "../../files/components";
import type { FileBrowserViewMode } from "../../files/hooks/useFilesLayout";
import { useFilePreview } from "../../files/hooks/useFilePreview";
import { SharedFilePreviewModal } from "./SharedFilePreviewModal";

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
  const { previewState, openPreview, closePreview } = useFilePreview();

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
      .map((file) => ({
        file,
        typeInfo: getFileTypeInfo(file.name, file.contentType),
      }))
      .filter(
        ({ typeInfo }) => typeInfo.type === "image" || typeInfo.type === "video",
      )
      .map(({ file, typeInfo }) => {
        const preview = getFileIcon(
          file.previewHashEncryptedHex ?? null,
          file.name,
          file.contentType,
        );
        const previewUrl = typeof preview === "string" ? preview : "";

        return {
          id: file.id,
          kind: typeInfo.type === "image" ? "image" : "video",
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

  const handleDownload = React.useCallback(
    async (fileId: string, fileName: string) => {
      try {
        await sharedFoldersApi.downloadFile(token, fileId, fileName);
      } catch {
        // ignore
      }
    },
    [token],
  );

  const handleFileClick = React.useCallback(
    (fileId: string, fileName: string, fileSizeBytes?: number) => {
      const file = content?.files.find((x) => x.id === fileId) ?? null;
      const typeInfo = getFileTypeInfo(fileName, file?.contentType ?? null);

      if (typeInfo.type === "image" || typeInfo.type === "video") {
        handleMediaClick(fileId);
        return;
      }

      if (typeInfo.type === "pdf" || typeInfo.type === "text") {
        const opened = openPreview(
          fileId,
          fileName,
          fileSizeBytes,
          file?.contentType ?? null,
        );
        if (opened) return;
      }

      void handleDownload(fileId, fileName);
    },
    [content?.files, handleDownload, handleMediaClick, openPreview],
  );

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
    onClick: (fileId: string, fileName: string, fileSizeBytes?: number) => {
      handleFileClick(fileId, fileName, fileSizeBytes);
    },
    onMediaClick: handleMediaClick,
  }), [handleDownload, handleFileClick, handleMediaClick]);

  const previewContentType = React.useMemo(() => {
    if (!previewState.fileId) return null;
    const file = content?.files.find((x) => x.id === previewState.fileId) ?? null;
    return file?.contentType ?? null;
  }, [content?.files, previewState.fileId]);

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

  const canGoUp = breadcrumbs.length > 1;

  return (
    <Box
      flex={1}
      minHeight={0}
      overflow="auto"
      px={{ xs: 2, sm: 3 }}
      pt={0}
      pb={2}
    >
      <PageHeader
        loading={loading}
        breadcrumbs={breadcrumbs}
        onNavigateBreadcrumb={(breadcrumbIndex) => {
          handleNavigateBreadcrumb(breadcrumbIndex);
        }}
        stats={stats}
        viewMode={viewMode as FileBrowserViewMode}
        canGoUp={canGoUp}
        onGoUp={() => {
          if (!canGoUp) return;
          handleNavigateBreadcrumb(breadcrumbs.length - 2);
        }}
        onHomeClick={() => handleNavigateBreadcrumb(0)}
        onViewModeCycle={cycleViewMode}
        showViewModeToggle
        statsNamespace="files"
      />

      <FileListViewFactory
        layoutType={layoutType}
        tiles={tiles}
        folderOperations={folderOperations}
        fileOperations={fileOperations}
        readOnly
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

      {lightboxOpen && mediaItems.length > 0 && (
        <MediaLightbox
          items={mediaItems}
          open={lightboxOpen}
          initialIndex={lightboxIndex}
          onClose={() => setLightboxOpen(false)}
          getSignedMediaUrl={getSignedMediaUrl}
          getDownloadUrl={getDownloadUrl}
        />
      )}

      <SharedFilePreviewModal
        open={previewState.isOpen}
        token={token}
        fileId={previewState.fileId}
        fileName={previewState.fileName}
        fileType={previewState.fileType}
        fileSizeBytes={previewState.fileSizeBytes}
        contentType={previewContentType}
        onClose={closePreview}
      />
    </Box>
  );
};
