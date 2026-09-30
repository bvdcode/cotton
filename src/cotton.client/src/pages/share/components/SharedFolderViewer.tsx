import * as React from "react";
import { Alert, Box, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { InterfaceLayoutType } from "../../../shared/api/layoutsApi";
import { PageHeader } from "../../files/components";
import { MediaLightbox } from "@shared/ui/preview";
import { FileListViewFactory } from "../../files/components/views/FileListViewFactory";
import type {
  FileOperations,
  FolderOperations,
  TilesSize,
} from "@shared/types/FileListViewTypes";
import { useContentTiles } from "../../../shared/hooks/useContentTiles";
import { getFileTypeInfo } from "@shared/utils/fileTypes";
import { sharedFoldersApi } from "../../../shared/api/sharedFoldersApi";
import { useSharedFolderContent } from "./useSharedFolderContent";
import { buildSharedMediaItems } from "./sharedFolderMedia";
import type { Guid } from "../../../shared/api/layoutsApi";
import type { FileBrowserViewMode } from "@shared/utils/viewMode";
import { useFilePreview } from "@shared/hooks/useFilePreview";
import { openDownloadLink } from "@shared/utils/fileHandlers";
import { toast } from "@shared/ui/notifications";
import { SharedFilePreviewModal } from "./SharedFilePreviewModal";
import {
  cycleFileBrowserViewMode,
  getFileBrowserViewMode,
} from "@shared/utils/viewMode";

interface BreadcrumbNode {
  id: Guid;
  name: string;
}

type BreadcrumbState = {
  rootNodeId: Guid;
  rootName: string;
  breadcrumbs: BreadcrumbNode[];
};

const createRootBreadcrumbState = (
  rootNodeId: Guid,
  rootName: string,
): BreadcrumbState => ({
  rootNodeId,
  rootName,
  breadcrumbs: [{ id: rootNodeId, name: rootName }],
});

interface SharedFolderViewerProps {
  token: string;
  rootNodeId: Guid;
  rootName: string;
  onDownloadActionChange?: (
    action: { disabled: boolean; onDownload: () => void } | null,
  ) => void;
}

export const SharedFolderViewer: React.FC<SharedFolderViewerProps> = ({
  token,
  rootNodeId,
  rootName,
  onDownloadActionChange,
}) => {
  const { t } = useTranslation(["share", "common"]);
  const rootBreadcrumbs = React.useMemo<BreadcrumbNode[]>(
    () => [{ id: rootNodeId, name: rootName }],
    [rootName, rootNodeId],
  );
  const [breadcrumbState, setBreadcrumbState] = React.useState<BreadcrumbState>(
    () => createRootBreadcrumbState(rootNodeId, rootName),
  );
  const breadcrumbs =
    breadcrumbState.rootNodeId === rootNodeId &&
    breadcrumbState.rootName === rootName
      ? breadcrumbState.breadcrumbs
      : rootBreadcrumbs;
  const [layoutType, setLayoutType] = React.useState<InterfaceLayoutType>(
    InterfaceLayoutType.Tiles,
  );
  const [tilesSize, setTilesSize] = React.useState<TilesSize>("medium");
  const [lightboxOpen, setLightboxOpen] = React.useState<boolean>(false);
  const [lightboxIndex, setLightboxIndex] = React.useState<number>(0);
  const [isDownloadingAll, setIsDownloadingAll] = React.useState(false);
  const { previewState, openPreview, closePreview } = useFilePreview();

  const currentNode = React.useMemo(
    () => breadcrumbs[breadcrumbs.length - 1] ?? null,
    [breadcrumbs],
  );
  const { content, loading, loadError } = useSharedFolderContent(
    token,
    currentNode?.id ?? null,
  );

  const handleOpenFolder = React.useCallback(
    (folderId: Guid, folderName: string) => {
      setBreadcrumbState((current) => {
        const base =
          current.rootNodeId === rootNodeId && current.rootName === rootName
            ? current
            : createRootBreadcrumbState(rootNodeId, rootName);
        return {
          ...base,
          breadcrumbs: [
            ...base.breadcrumbs,
            { id: folderId, name: folderName },
          ],
        };
      });
    },
    [rootName, rootNodeId],
  );

  const handleNavigateBreadcrumb = React.useCallback(
    (index: number) => {
      setBreadcrumbState((current) => {
        const base =
          current.rootNodeId === rootNodeId && current.rootName === rootName
            ? current
            : createRootBreadcrumbState(rootNodeId, rootName);
        return {
          ...base,
          breadcrumbs: base.breadcrumbs.slice(0, index + 1),
        };
      });
    },
    [rootName, rootNodeId],
  );

  const viewMode: FileBrowserViewMode = React.useMemo(
    () => getFileBrowserViewMode(layoutType, tilesSize),
    [layoutType, tilesSize],
  );

  const cycleViewMode = React.useCallback(() => {
    cycleFileBrowserViewMode(viewMode, setLayoutType, setTilesSize);
  }, [setLayoutType, setTilesSize, viewMode]);

  const { sortedFiles, tiles } = useContentTiles(content ?? undefined);

  const mediaItems = React.useMemo(
    () => buildSharedMediaItems(sortedFiles),
    [sortedFiles],
  );

  const handleMediaClick = React.useCallback(
    (fileId: string) => {
      const mediaIndex = mediaItems.findIndex((item) => item.id === fileId);
      if (mediaIndex < 0) {
        return;
      }

      setLightboxIndex(mediaIndex);
      setLightboxOpen(true);
    },
    [mediaItems],
  );

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

  const handleDownloadCurrentFolder = React.useCallback(async () => {
    if (!currentNode || isDownloadingAll) return;

    setIsDownloadingAll(true);
    try {
      const archive = await sharedFoldersApi.createArchiveDownloadLink(
        token,
        currentNode.id,
      );
      openDownloadLink(archive.url, archive.fileName);
    } catch {
      toast.error(t("folder.downloadFailed", { ns: "share" }), {
        toastId: "shared-folder-download-failed",
      });
    } finally {
      setIsDownloadingAll(false);
    }
  }, [currentNode, isDownloadingAll, t, token]);

  React.useEffect(() => {
    if (!onDownloadActionChange) return;

    if (!currentNode) {
      onDownloadActionChange(null);
      return;
    }

    onDownloadActionChange({
      disabled: loading || isDownloadingAll,
      onDownload: handleDownloadCurrentFolder,
    });
  }, [
    currentNode,
    handleDownloadCurrentFolder,
    isDownloadingAll,
    loading,
    onDownloadActionChange,
  ]);

  React.useEffect(() => {
    return () => {
      onDownloadActionChange?.(null);
    };
  }, [onDownloadActionChange]);

  const handleFileClick = React.useCallback(
    (fileId: string, fileName: string, fileSizeBytes?: number) => {
      const file = content?.files.find((x) => x.id === fileId) ?? null;
      const typeInfo = getFileTypeInfo(fileName, file?.contentType ?? null);

      if (typeInfo.type === "image" || typeInfo.type === "video") {
        handleMediaClick(fileId);
        return;
      }

      openPreview(fileId, fileName, fileSizeBytes, file?.contentType ?? null);
    },
    [content?.files, handleMediaClick, openPreview],
  );

  const fileOperations = React.useMemo<FileOperations>(
    () => ({
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
    }),
    [handleDownload, handleFileClick, handleMediaClick],
  );

  const previewContentType = React.useMemo(() => {
    if (!previewState.fileId) return null;
    const file =
      content?.files.find((x) => x.id === previewState.fileId) ?? null;
    return file?.contentType ?? null;
  }, [content?.files, previewState.fileId]);

  const folderOperations = React.useMemo<FolderOperations>(
    () => ({
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
    }),
    [content?.nodes, handleOpenFolder],
  );

  const stats = React.useMemo(() => {
    const foldersCount = content?.nodes.length ?? 0;
    const filesCount = content?.files.length ?? 0;
    const sizeBytes = (content?.files ?? []).reduce(
      (acc, file) => acc + file.sizeBytes,
      0,
    );

    return {
      folders: foldersCount,
      files: filesCount,
      sizeBytes,
    };
  }, [content?.files, content?.nodes.length]);

  const getSignedMediaUrl = React.useCallback(
    async (fileId: string): Promise<string> => {
      return `${sharedFoldersApi.buildFileContentUrl(token, fileId, "inline")}&preview=true`;
    },
    [token],
  );

  const getDownloadUrl = React.useCallback(
    async (fileId: string): Promise<string> => {
      return sharedFoldersApi.buildFileContentUrl(token, fileId, "download");
    },
    [token],
  );

  if (loading) {
    return (
      <Box
        flex={1}
        minHeight={0}
        display="flex"
        alignItems="center"
        justifyContent="center"
      >
        <Typography color="text.secondary">
          {t("loading", { ns: "share" })}
        </Typography>
      </Box>
    );
  }

  if (loadError) {
    return (
      <Box
        flex={1}
        minHeight={0}
        display="flex"
        alignItems="center"
        justifyContent="center"
        p={2}
      >
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
        folderNamePlaceholder={t("actions.folderNamePlaceholder", {
          ns: "files",
        })}
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
        onDownload={() => {
          if (previewState.fileId && previewState.fileName) {
            void handleDownload(previewState.fileId, previewState.fileName);
          }
        }}
      />
    </Box>
  );
};
