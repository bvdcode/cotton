import React, { useEffect, useMemo } from "react";
import {
  Alert,
  Box,
  IconButton,
  LinearProgress,
  Dialog,
  DialogContent,
  DialogTitle,
  Snackbar,
} from "@mui/material";
import {
  FileListViewFactory,
  PageHeader,
  MediaLightbox,
  FilePreviewModal,
} from "../files/components";
import { Delete } from "@mui/icons-material";
import { useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import Loader from "../../shared/ui/Loader";
import { nodesApi } from "../../shared/api/nodesApi";
import type { NodeContentDto } from "../../shared/api/nodesApi";
import { useTrashStore } from "../../shared/store/trashStore";
import { useTrashFolderOperations } from "./hooks/useTrashFolderOperations";
import { useTrashFileOperations } from "./hooks/useTrashFileOperations";
import { useFilePreview } from "../files/hooks/useFilePreview";
import { useMediaLightbox } from "../files/hooks/useMediaLightbox";
import { downloadFile } from "../files/utils/fileHandlers";
import {
  buildBreadcrumbs,
  calculateFolderStats,
} from "../files/utils/nodeUtils";
import { useContentTiles } from "../../shared/hooks/useContentTiles";
import { useTrashFileList } from "../../shared/hooks/useFileListSource";
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import { shareFile } from "../../shared/utils/shareFile";
import { shareFolder } from "../../shared/utils/shareFolder";
import { filesApi } from "../../shared/api/filesApi";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import {
  selectTrashLayoutType,
  selectTrashTilesSize,
  selectGallerySmoothTransitions,
  useLocalPreferencesStore,
} from "../../shared/store/localPreferencesStore";
import type { TilesSize } from "../files/types/FileListViewTypes";
import type { FileBrowserViewMode } from "../files/hooks/useFilesLayout";

function getTrashViewMode(args: {
  layoutType: InterfaceLayoutType;
  tilesSize: TilesSize;
}): FileBrowserViewMode {
  const { layoutType, tilesSize } = args;

  if (layoutType === InterfaceLayoutType.List) return "table";

  if (tilesSize === "small") return "tiles-small";
  if (tilesSize === "large") return "tiles-large";
  return "tiles-medium";
}

async function deleteAllTrashItems(args: {
  content: NodeContentDto;
  isTrashRoot: boolean;
  onProgress: (current: number, total: number) => void;
}): Promise<void> {
  const { content, isTrashRoot, onProgress } = args;

  if (isTrashRoot) {
    // Collect unique wrapper node IDs from the unwrapped content.
    // For nodes: parentId is the wrapper; for files: nodeId is the wrapper.
    const wrapperIds = new Set<string>();
    for (const node of content.nodes ?? []) {
      if (node.parentId) wrapperIds.add(node.parentId);
    }
    for (const file of content.files ?? []) {
      if (file.nodeId) wrapperIds.add(file.nodeId);
    }

    const wrapperArray = [...wrapperIds];
    let deleted = 0;

    for (const wrapperId of wrapperArray) {
      try {
        await nodesApi.deleteNode(wrapperId, true);
        deleted += 1;
        onProgress(deleted, wrapperArray.length);
      } catch (err) {
        console.error(`Failed to delete trash wrapper ${wrapperId}:`, err);
      }
    }
  } else {
    const totalItems =
      (content.nodes?.length ?? 0) + (content.files?.length ?? 0);

    let deleted = 0;

    for (const folder of content.nodes ?? []) {
      try {
        await nodesApi.deleteNode(folder.id, true);
        deleted += 1;
        onProgress(deleted, totalItems);
      } catch (err) {
        console.error(`Failed to delete folder ${folder.id}:`, err);
      }
    }

    for (const file of content.files ?? []) {
      try {
        await filesApi.deleteFile(file.id, true);
        deleted += 1;
        onProgress(deleted, totalItems);
      } catch (err) {
        console.error(`Failed to delete file ${file.id}:`, err);
      }
    }
  }
}

type ShareToastState = {
  open: boolean;
  message: string;
};

type ShareToastSnackbarProps = {
  toast: ShareToastState;
  onClose: () => void;
};

const ShareToastSnackbar: React.FC<ShareToastSnackbarProps> = ({
  toast,
  onClose,
}) => {
  return (
    <Snackbar
      open={toast.open}
      autoHideDuration={2500}
      onClose={onClose}
      message={toast.message}
    />
  );
};

type EmptyTrashProgressDialogProps = {
  open: boolean;
  title: string;
  progressPercent: number;
};

const EmptyTrashProgressDialog: React.FC<EmptyTrashProgressDialogProps> = ({
  open,
  title,
  progressPercent,
}) => {
  if (!open) return null;

  return (
    <Dialog open={open} disableEscapeKeyDown>
      <DialogTitle sx={{ fontFamily: "monospace" }}>{title}</DialogTitle>
      <DialogContent>
        <LinearProgress variant="determinate" value={progressPercent} />
      </DialogContent>
    </Dialog>
  );
};

export const TrashPage: React.FC = () => {
  const { t } = useTranslation(["trash", "common"]);
  const navigate = useNavigate();
  const params = useParams<{ nodeId?: string }>();
  const confirm = useConfirm();

  const {
    currentNode,
    ancestors,
    contentByNodeId,
    loading,
    error,
    loadRoot,
    loadNode,
    refreshNodeContent,
  } = useTrashStore();

  const routeNodeId = params.nodeId;

  const storedLayoutType = useLocalPreferencesStore(selectTrashLayoutType);
  const layoutType = storedLayoutType ?? InterfaceLayoutType.Tiles;
  const tilesSize = useLocalPreferencesStore(selectTrashTilesSize) as TilesSize;
  const smoothGalleryTransitions = useLocalPreferencesStore(
    selectGallerySmoothTransitions,
  );
  const setLayoutType = useLocalPreferencesStore((s) => s.setTrashLayoutType);
  const setTilesSize = useLocalPreferencesStore((s) => s.setTrashTilesSize);

  const viewMode = getTrashViewMode({ layoutType, tilesSize });

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
        return;
      default:
        setLayoutType(InterfaceLayoutType.List);
    }
  }, [setLayoutType, setTilesSize, viewMode]);

  const [listTotalCount, setListTotalCount] = React.useState(0);
  const [, setListLoading] = React.useState(false);
  const [listError, setListError] = React.useState<string | null>(null);
  const [listContent, setListContent] = React.useState<NodeContentDto | null>(
    null,
  );
  const [currentPagination, setCurrentPagination] = React.useState<{
    page: number;
    pageSize: number;
  } | null>(null);

  const [emptyingTrash, setEmptyingTrash] = React.useState(false);
  const [emptyTrashProgress, setEmptyTrashProgress] = React.useState({
    current: 0,
    total: 0,
  });

  useEffect(() => {
    const loadChildren = layoutType !== InterfaceLayoutType.List;
    if (!routeNodeId) {
      void loadRoot({ force: false, loadChildren });
    } else {
      void loadNode(routeNodeId, { loadChildren });
    }
  }, [routeNodeId, layoutType, loadRoot, loadNode]);

  const nodeId = routeNodeId ?? currentNode?.id ?? null;
  const content = nodeId ? contentByNodeId[nodeId] : undefined;

  const fetchListPage = React.useCallback(
    async (page: number, pageSize: number) => {
      if (!nodeId) return;

      setListLoading(true);
      setListError(null);
      try {
        const response = await nodesApi.getChildren(nodeId, {
          nodeType: "trash",
          page: page + 1,
          pageSize,
          depth: !routeNodeId ? 1 : 0,
        });
        setListContent(response.content);
        setListTotalCount(response.totalCount);
      } catch (err) {
        console.error("Failed to load paged trash content", err);
        setListError(t("error"));
      } finally {
        setListLoading(false);
      }
    },
    [nodeId, routeNodeId, t],
  );

  useEffect(() => {
    if (
      layoutType === InterfaceLayoutType.List &&
      nodeId &&
      currentPagination
    ) {
      void fetchListPage(currentPagination.page, currentPagination.pageSize);
    }
  }, [nodeId, layoutType, currentPagination, fetchListPage]);

  // Auto-init pagination when switching to list mode
  useEffect(() => {
    if (layoutType !== InterfaceLayoutType.List) {
      setListContent(null);
      setListError(null);
      setCurrentPagination(null);
      return;
    }
    if (nodeId && !currentPagination) {
      setCurrentPagination({ page: 0, pageSize: 100 });
    }
  }, [nodeId, layoutType, currentPagination]);

  const handlePaginationChange = React.useCallback(
    (page: number, pageSize: number) => {
      setCurrentPagination({ page, pageSize });
      void fetchListPage(page, pageSize);
    },
    [fetchListPage],
  );
  const refreshContent = React.useCallback(async () => {
    if (!nodeId) return;
    void refreshNodeContent(nodeId);
  }, [nodeId, refreshNodeContent]);

  useEffect(() => {
    const folderName = currentNode?.name;
    const isRoot = !routeNodeId || ancestors.length === 0;

    if (isRoot) {
      document.title = "Cotton - Trash";
    } else if (folderName) {
      document.title = `Cotton - ${folderName}`;
    } else {
      document.title = "Cotton";
    }

    return () => {
      document.title = "Cotton";
    };
  }, [currentNode?.name, routeNodeId, ancestors.length]);

  const breadcrumbs = useMemo(
    () => buildBreadcrumbs(ancestors, currentNode),
    [ancestors, currentNode],
  );

  const effectiveContent =
    layoutType === InterfaceLayoutType.List
      ? (listContent ?? content)
      : content;

  useTrashFileList({
    nodeId,
    layoutType,
    listContent,
  });

  const { sortedFiles, tiles } = useContentTiles(effectiveContent);

  const isTrashRoot = !routeNodeId;

  const resolveWrapperNodeId = React.useCallback(
    (itemId: string): string | null => {
      if (!isTrashRoot || !effectiveContent) return null;
      const node = effectiveContent.nodes?.find((n) => n.id === itemId);
      if (node?.parentId) return node.parentId;
      const file = effectiveContent.files?.find((f) => f.id === itemId);
      if (file?.nodeId) return file.nodeId;
      return null;
    },
    [isTrashRoot, effectiveContent],
  );

  const folderOps = useTrashFolderOperations(
    nodeId,
    refreshContent,
    isTrashRoot ? resolveWrapperNodeId : undefined,
  );
  const fileOps = useTrashFileOperations(
    refreshContent,
    isTrashRoot ? resolveWrapperNodeId : undefined,
  );
  const { previewState, openPreview, closePreview } = useFilePreview();

  const {
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    getDownloadUrl,
    handleMediaClick,
    setLightboxOpen,
  } = useMediaLightbox(sortedFiles);

  const stats = useMemo(
    () => calculateFolderStats(content?.nodes, content?.files),
    [content?.files, content?.nodes],
  );

  const goToFolder = useMemo(
    () => (folderId: string) => navigate(`/trash/${folderId}`),
    [navigate],
  );

  const goHome = useMemo(() => () => navigate("/trash"), [navigate]);

  const handleGoUp = React.useCallback(() => {
    if (ancestors.length > 0) {
      const parent = ancestors[ancestors.length - 1];
      navigate(`/trash/${parent.id}`);
    } else {
      navigate("/trash");
    }
  }, [ancestors, navigate]);

  const handleEmptyTrash = React.useCallback(async () => {
    if (!content) return;

    const totalItems =
      (content.nodes?.length ?? 0) + (content.files?.length ?? 0);
    if (totalItems === 0) return;

    try {
      const result = await confirm({
        title: t("emptyTrash.confirmTitle"),
        description: t("emptyTrash.confirmDescription"),
        confirmationText: t("common:actions.delete"),
        cancellationText: t("common:actions.cancel"),
        confirmationButtonProps: { color: "error" },
      });

      if (!result.confirmed) return;

      setEmptyingTrash(true);
      setEmptyTrashProgress({ current: 0, total: totalItems });

      await deleteAllTrashItems({
        content,
        isTrashRoot,
        onProgress: (current, total) =>
          setEmptyTrashProgress({ current, total }),
      });

      setEmptyingTrash(false);
      await refreshContent();
    } catch {
      setEmptyingTrash(false);
    }
  }, [confirm, content, isTrashRoot, refreshContent, t]);

  const handleDownloadFile = async (nodeFileId: string, fileName: string) => {
    await downloadFile(nodeFileId, fileName);
  };

  const [shareToast, setShareToast] = React.useState<ShareToastState>({
    open: false,
    message: "",
  });

  const handleShareFile = React.useCallback(
    async (nodeFileId: string, fileName: string) => {
      await shareFile(nodeFileId, fileName, t, setShareToast);
    },
    [t],
  );

  const handleShareFolder = React.useCallback(
    async (nodeId: string, folderName: string) => {
      await shareFolder(nodeId, folderName, t, setShareToast);
    },
    [t],
  );

  const handleFileClick = (
    fileId: string,
    fileName: string,
    fileSizeBytes?: number,
  ) => {
    const opened = openPreview(fileId, fileName, fileSizeBytes);
    if (!opened) {
      void handleDownloadFile(fileId, fileName);
    }
  };

  const folderOperations = buildFolderOperations(folderOps, {
    onFolderClick: goToFolder,
    onShare: handleShareFolder,
  });

  const fileOperations = buildFileOperations(fileOps, {
    onDownload: handleDownloadFile,
    onShare: handleShareFile,
    onClick: handleFileClick,
    onMediaClick: handleMediaClick,
  });

  const isCreatingInThisFolder = false;

  const pageHeaderProps = useMemo(
    (): React.ComponentProps<typeof PageHeader> => ({
      loading: layoutType !== InterfaceLayoutType.List && loading,
      breadcrumbs,
      stats,
      viewMode,
      canGoUp: ancestors.length > 0,
      onGoUp: handleGoUp,
      onHomeClick: goHome,
      onViewModeCycle: cycleViewMode,
      statsNamespace: "trash",
      customActions:
        ancestors.length === 0 ? (
          <IconButton
            onClick={handleEmptyTrash}
            color="error"
            disabled={
              loading || emptyingTrash || stats.folders + stats.files === 0
            }
            title={t("actions.emptyTrash")}
          >
            <Delete />
          </IconButton>
        ) : null,
    }),
    [
      ancestors.length,
      breadcrumbs,
      cycleViewMode,
      emptyingTrash,
      goHome,
      handleEmptyTrash,
      handleGoUp,
      layoutType,
      loading,
      stats,
      t,
      viewMode,
    ],
  );

  const onPaginationModelChange = useMemo(
    () => (model: { page: number; pageSize: number }) => {
      handlePaginationChange(model.page, model.pageSize);
    },
    [handlePaginationChange],
  );

  const fileListViewProps = useMemo(
    (): React.ComponentProps<typeof FileListViewFactory> => ({
      layoutType,
      tiles,
      folderOperations,
      fileOperations,
      isCreatingFolder: isCreatingInThisFolder,
      tileSize: tilesSize,
      loading:
        layoutType === InterfaceLayoutType.List
          ? !listContent && !listError
          : !content && !error,
      loadingTitle: t("loading.title"),
      loadingCaption: t("loading.caption"),
      emptyStateText: layoutType === InterfaceLayoutType.Tiles ? t("empty") : undefined,
      newFolderName: "",
      onNewFolderNameChange: () => {},
      onConfirmNewFolder: () => Promise.resolve(),
      onCancelNewFolder: () => {},
      folderNamePlaceholder: "",
      fileNamePlaceholder: "File name",
      pagination:
        layoutType === InterfaceLayoutType.List
          ? {
              totalCount: listTotalCount,
              loading: !listContent && !listError,
              onPaginationModelChange,
            }
          : undefined,
    }),
    [
      content,
      error,
      fileOperations,
      folderOperations,
      isCreatingInThisFolder,
      layoutType,
      listContent,
      listError,
      listTotalCount,
      onPaginationModelChange,
      t,
      tiles,
      tilesSize,
    ],
  );

  if (!content && !error && layoutType !== InterfaceLayoutType.List) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

  return (
    <>
      <ShareToastSnackbar
        toast={shareToast}
        onClose={() => setShareToast((prev) => ({ ...prev, open: false }))}
      />
      <Box
        width="100%"
        sx={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          flex: 1,
          minHeight: 0,
        }}
      >
        <PageHeader {...pageHeaderProps} />
        {(error || listError) && (
          <Box mb={1} px={1}>
            <Alert severity="error">{error ?? listError}</Alert>
          </Box>
        )}

        <Box
          sx={
            layoutType === InterfaceLayoutType.List
              ? { flex: 1, minHeight: 0, overflow: "hidden", pb: 1 }
              : { pb: { xs: 1, sm: 2 } }
          }
        >
          <FileListViewFactory {...fileListViewProps} />
        </Box>
      </Box>

      <FilePreviewModal
        isOpen={previewState.isOpen}
        fileId={previewState.fileId}
        fileName={previewState.fileName}
        fileType={previewState.fileType}
        fileSizeBytes={previewState.fileSizeBytes}
        onClose={closePreview}
      />

      {lightboxOpen && mediaItems.length > 0 && (
        <MediaLightbox
          items={mediaItems}
          open={lightboxOpen}
          initialIndex={lightboxIndex}
          onClose={() => setLightboxOpen(false)}
          getSignedMediaUrl={getSignedMediaUrl}
          getDownloadUrl={getDownloadUrl}
          smoothTransitions={smoothGalleryTransitions}
        />
      )}

      <EmptyTrashProgressDialog
        open={emptyingTrash}
        title={t("emptyTrash.inProgress", {
          current: emptyTrashProgress.current,
          total: emptyTrashProgress.total,
        })}
        progressPercent={
          emptyTrashProgress.total > 0
            ? (emptyTrashProgress.current / emptyTrashProgress.total) * 100
            : 0
        }
      />
    </>
  );
};
