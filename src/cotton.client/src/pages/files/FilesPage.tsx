import React, { useDeferredValue, useEffect, useMemo } from "react";
import { Alert, Box } from "@mui/material";
import { ContentCut, ContentPaste, Delete } from "@mui/icons-material";
import { toast } from "react-toastify";
import {
  FileListViewFactory,
  PageHeader,
  MediaLightbox,
  FilePreviewModal,
  FileConflictDialog,
  DraggingOverlay,
} from "./components";
import { useNavigate, useParams, useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useNodesStore } from "../../shared/store/nodesStore";
import { useAuthStore } from "../../shared/store/authStore";
import { useFolderOperations } from "./hooks/useFolderOperations";
import { useFileUpload } from "./hooks/useFileUpload";
import { useFileOperations } from "./hooks/useFileOperations";
import { useFileInteractionHandlers } from "./hooks/useFileInteractionHandlers";
import { useFilesLayout } from "./hooks/useFilesLayout";
import { useFilesData } from "./hooks/useFilesData";
import { useFilesRealtimeEvents } from "./hooks/useFilesRealtimeEvents";
import { useFileSelection } from "./hooks/useFileSelection";
import { useDeleteSelectedItems } from "./hooks/useDeleteSelectedItems";
import { buildBreadcrumbs, calculateFolderStats } from "./utils/nodeUtils";
import { getFileTypeInfo } from "./utils/fileTypes";
import {
  getDropPreparationCaption,
  getDropPreparationTitle,
} from "./utils/dropPreparation";
import { useContentTiles } from "../../shared/hooks/useContentTiles";
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { shareFolder } from "../../shared/utils/shareFolder";
import Loader from "../../shared/ui/Loader";
import { useAudioPlayerStore } from "../../shared/store/audioPlayerStore";
import {
  selectGallerySmoothTransitions,
  useLocalPreferencesStore,
} from "../../shared/store/localPreferencesStore";
import { usePageTitle } from "../../shared/hooks/usePageTitle";
import {
  useMoveOperations,
  isMoveDrag,
  getMoveDragSourceParents,
  readMoveDragPayload,
} from "../../shared/hooks/useMoveOperations";
import {
  useMoveClipboardStore,
  type MoveClipboardItem,
} from "../../shared/store/moveClipboardStore";

const HUGE_FOLDER_THRESHOLD = 100_000;

export const FilesPage: React.FC = () => {
  const { t } = useTranslation(["files", "common"]);
  const confirm = useConfirm();
  const navigate = useNavigate();
  const location = useLocation();
  const params = useParams<{ nodeId?: string }>();
  const pendingSelectedFileIdRef = React.useRef<string | null>(
    (location.state as { selectedFileId?: string } | null)?.selectedFileId ?? null,
  );

  const {
    currentNode,
    ancestors,
    contentByNodeId,
    cacheOwnerUserId,
    rootNodeId,
    loading,
    error,
    loadRoot,
    loadNode,
    resolveRootInBackground,
    refreshNodeContent,
    deleteFolder,
    optimisticDeleteFile,
  } = useNodesStore();
  const currentUserId = useAuthStore((s) => s.user?.id ?? null);

  const routeNodeId = params.nodeId;

  const { layoutType, setLayoutType, tilesSize, viewMode, cycleViewMode } =
    useFilesLayout();

  // Resolve root node ID on cold start (home route with no persisted root)
  useEffect(() => {
    if (routeNodeId || rootNodeId) return;
    void loadRoot({ force: false, loadChildren: false });
  }, [routeNodeId, rootNodeId, loadRoot]);

  // Always keep root node synced with backend resolver (non-blocking).
  useEffect(() => {
    if (routeNodeId) return;
    resolveRootInBackground({
      loadChildren: layoutType !== InterfaceLayoutType.List,
    });
  }, [routeNodeId, layoutType, resolveRootInBackground]);

  const nodeId = routeNodeId ?? rootNodeId ?? null;
  const isUserCacheValid = cacheOwnerUserId === currentUserId;
  const content = nodeId && isUserCacheValid ? contentByNodeId[nodeId] : undefined;

  const {
    childrenTotalCount,
    listError,
    listContent,
    handleFolderChanged,
    reloadCurrentNode,
    optimisticUpdateCurrentNodeFilePreviewHash,
  } = useFilesData({
    nodeId,
    layoutType,
    loadNode,
    refreshNodeContent,
  });

  useFilesRealtimeEvents({
    nodeId,
    onInvalidate: reloadCurrentNode,
    onPreviewGenerated: optimisticUpdateCurrentNodeFilePreviewHash,
  });

  const isHugeFolder =
    childrenTotalCount !== null && childrenTotalCount > HUGE_FOLDER_THRESHOLD;

  useEffect(() => {
    if (!isHugeFolder) return;
    if (layoutType === InterfaceLayoutType.List) return;
    setLayoutType(InterfaceLayoutType.List);
  }, [isHugeFolder, layoutType, setLayoutType]);

  const pageTitle = useMemo(() => {
    const folderName = currentNode?.name;
    const isRoot = !routeNodeId || ancestors.length === 0;

    if (isRoot) {
      return t("title", { ns: "files" });
    }

    return folderName ?? null;
  }, [currentNode?.name, routeNodeId, ancestors.length, t]);

  usePageTitle(pageTitle);

  const breadcrumbs = useMemo(
    () => buildBreadcrumbs(ancestors, currentNode),
    [ancestors, currentNode],
  );

  const effectiveContent =
    layoutType === InterfaceLayoutType.List ? listContent : content;

  const deferredContent = useDeferredValue(effectiveContent);
  const isContentTransitioning =
    !!effectiveContent && deferredContent !== effectiveContent;

  const { sortedFiles, tiles } = useContentTiles(deferredContent ?? undefined);

  const setScanRootNodeId = useAudioPlayerStore((s) => s.setScanRootNodeId);

  useEffect(() => {
    if (!nodeId) return;
    setScanRootNodeId(nodeId);
  }, [nodeId, setScanRootNodeId]);

  const {
    previewState,
    closePreview,
    handleFileClick,
    handleDownloadFile,
    handleShareFile,
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    getDownloadUrl,
    handleMediaClick,
    setLightboxOpen,
  } = useFileInteractionHandlers({
    sortedFiles,
  });

  // Consume selectedFileId from router state (e.g. dashboard → open file)
  React.useEffect(() => {
    const targetId = pendingSelectedFileIdRef.current;
    if (!targetId || sortedFiles.length === 0) return;

    const file = sortedFiles.find((f) => f.id === targetId);
    if (!file) return;

    pendingSelectedFileIdRef.current = null;
    window.history.replaceState({}, "");

    const typeInfo = getFileTypeInfo(file.name, file.contentType ?? null);
    if (typeInfo.type === "image" || typeInfo.type === "video") {
      handleMediaClick(file.id);
    } else {
      handleFileClick(file.id, file.name, file.sizeBytes);
    }
  }, [sortedFiles, handleFileClick, handleMediaClick]);

  const showToast = React.useCallback(
    (message: string, variant: "info" | "error" = "info") => {
      const toastId = `files-upload-${variant}-${message}`;
      if (variant === "error") {
        toast.error(message, { toastId });
        return;
      }

      toast.info(message, { toastId });
    },
    [],
  );

  const folderOps = useFolderOperations(nodeId, handleFolderChanged);
  const fileUpload = useFileUpload(nodeId, breadcrumbs, content, {
    onToast: showToast,
  });
  const fileOps = useFileOperations(reloadCurrentNode);
  const fileSelection = useFileSelection();

  const moveOps = useMoveOperations();
  const clipboardItems = useMoveClipboardStore((s) => s.items);
  const cutItemIds = useMemo(
    () => new Set(clipboardItems.map((c) => c.id)),
    [clipboardItems],
  );

  const buildClipboardItemsFromIds = React.useCallback(
    (ids: Iterable<string>): MoveClipboardItem[] => {
      if (!nodeId) return [];
      const items: MoveClipboardItem[] = [];
      const idsSet = new Set(ids);
      for (const tile of tiles) {
        if (tile.kind === "folder") {
          if (!idsSet.has(tile.node.id)) continue;
          items.push({
            id: tile.node.id,
            kind: "folder",
            name: tile.node.name,
            sourceParentId: tile.node.parentId ?? nodeId,
          });
        } else {
          if (!idsSet.has(tile.file.id)) continue;
          items.push({
            id: tile.file.id,
            kind: "file",
            name: tile.file.name,
            sourceParentId: tile.file.nodeId ?? nodeId,
          });
        }
      }
      return items;
    },
    [nodeId, tiles],
  );

  const handleCutSelection = React.useCallback(() => {
    if (fileSelection.selectedCount === 0) return;
    const items = buildClipboardItemsFromIds(fileSelection.selectedIds);
    if (items.length === 0) return;
    moveOps.cutItems(items);
    showToast(t("move.toasts.cut", { ns: "files", count: items.length }));
  }, [
    buildClipboardItemsFromIds,
    fileSelection.selectedCount,
    fileSelection.selectedIds,
    moveOps,
    showToast,
    t,
  ]);

  const handlePasteHere = React.useCallback(() => {
    if (!nodeId) return;
    if (clipboardItems.length === 0) return;
    void moveOps.pasteInto(nodeId);
  }, [clipboardItems.length, moveOps, nodeId]);

  const handleMoveItems = React.useCallback(
    (
      items: ReadonlyArray<MoveClipboardItem>,
      targetParentId: string,
    ): void => {
      void moveOps.moveItems(items, targetParentId);
    },
    [moveOps],
  );

  // Global Ctrl+X / Ctrl+V hotkeys
  React.useEffect(() => {
    const isEditableTarget = (target: EventTarget | null): boolean => {
      if (!(target instanceof HTMLElement)) return false;
      if (target.isContentEditable) return true;
      const tag = target.tagName;
      return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT";
    };

    const handler = (event: KeyboardEvent) => {
      if (!(event.ctrlKey || event.metaKey)) return;
      const key = event.key.toLowerCase();
      if (key !== "x" && key !== "v") return;
      if (isEditableTarget(event.target)) return;

      if (key === "x") {
        if (fileSelection.selectedCount === 0) return;
        event.preventDefault();
        handleCutSelection();
      } else if (key === "v") {
        if (clipboardItems.length === 0) return;
        if (!nodeId) return;
        event.preventDefault();
        handlePasteHere();
      }
    };

    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [
    clipboardItems.length,
    fileSelection.selectedCount,
    handleCutSelection,
    handlePasteHere,
    nodeId,
  ]);

  // Drop handlers for the "Go up" action and breadcrumbs
  const [goUpDropActive, setGoUpDropActive] = React.useState(false);
  const goUpParentId = ancestors.length > 0
    ? ancestors[ancestors.length - 1].id
    : null;

  const readDropPayload = React.useCallback(
    (event: React.DragEvent<HTMLElement>): MoveClipboardItem[] | null => {
      const payload = readMoveDragPayload(event.dataTransfer);
      return payload ? [...payload.items] : null;
    },
    [],
  );

  const canAcceptDropOn = React.useCallback(
    (event: React.DragEvent<HTMLElement>, targetParentId: string): boolean => {
      if (!isMoveDrag(event.dataTransfer)) return false;
      const sources = getMoveDragSourceParents(event.dataTransfer);
      // Reject drops onto the source parent (no-op move).
      return !sources.has(targetParentId);
    },
    [],
  );

  const goUpDropHandlers = React.useMemo(() => {
    if (!goUpParentId) return undefined;
    return {
      onDragOver: (event: React.DragEvent<HTMLElement>) => {
        if (!canAcceptDropOn(event, goUpParentId)) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = "move";
        if (!goUpDropActive) setGoUpDropActive(true);
      },
      onDragLeave: (event: React.DragEvent<HTMLElement>) => {
        const related = event.relatedTarget as Node | null;
        if (related && event.currentTarget.contains(related)) return;
        setGoUpDropActive(false);
      },
      onDrop: (event: React.DragEvent<HTMLElement>) => {
        setGoUpDropActive(false);
        if (!isMoveDrag(event.dataTransfer)) return;
        event.preventDefault();
        event.stopPropagation();
        const items = readDropPayload(event);
        if (!items || items.length === 0) return;
        handleMoveItems(items, goUpParentId);
      },
      active: goUpDropActive,
    };
  }, [
    canAcceptDropOn,
    goUpDropActive,
    goUpParentId,
    handleMoveItems,
    readDropPayload,
  ]);

  const breadcrumbsDropHandlers = React.useMemo(() => {
    return {
      canAccept: (targetCrumbId: string) => targetCrumbId !== nodeId,
      onDragOver: (
        targetCrumbId: string,
        event: React.DragEvent<HTMLElement>,
      ) => {
        if (!canAcceptDropOn(event, targetCrumbId)) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = "move";
      },
      onDrop: (
        targetCrumbId: string,
        event: React.DragEvent<HTMLElement>,
      ) => {
        if (!isMoveDrag(event.dataTransfer)) return;
        event.preventDefault();
        event.stopPropagation();
        const items = readDropPayload(event);
        if (!items || items.length === 0) return;
        handleMoveItems(items, targetCrumbId);
      },
    };
  }, [canAcceptDropOn, handleMoveItems, nodeId, readDropPayload]);

  const moveSupport = useMemo(() => {
    if (!nodeId) return undefined;
    return {
      cutItemIds,
      currentParentId: nodeId,
      onMove: handleMoveItems,
    };
  }, [cutItemIds, handleMoveItems, nodeId]);

  const smoothGalleryTransitions = useLocalPreferencesStore(
    selectGallerySmoothTransitions,
  );

  const stats = useMemo(
    () => calculateFolderStats(deferredContent?.nodes, deferredContent?.files),
    [deferredContent?.files, deferredContent?.nodes],
  );

  const goToFolder = useMemo(
    () => (folderId: string) => navigate(`/files/${folderId}`),
    [navigate],
  );

  const goHome = useMemo(() => () => navigate("/files"), [navigate]);

  const handleGoUp = React.useCallback(() => {
    if (ancestors.length > 0) {
      const parent = ancestors[ancestors.length - 1];
      navigate(`/files/${parent.id}`);
    } else {
      navigate("/files");
    }
  }, [ancestors, navigate]);

  const handleShareFolder = React.useCallback(
    async (folderId: string, folderName: string) => {
      await shareFolder(folderId, folderName, t);
    },
    [t],
  );

  const handleCutFolder = React.useCallback(
    (folderId: string, folderName: string) => {
      if (!nodeId) return;
      const item: MoveClipboardItem = {
        id: folderId,
        kind: "folder",
        name: folderName,
        sourceParentId: nodeId,
      };
      moveOps.cutItems([item]);
      showToast(t("move.toasts.cut", { ns: "files", count: 1 }));
    },
    [moveOps, nodeId, showToast, t],
  );

  const handleCutFile = React.useCallback(
    (fileId: string, fileName: string) => {
      if (!nodeId) return;
      const item: MoveClipboardItem = {
        id: fileId,
        kind: "file",
        name: fileName,
        sourceParentId: nodeId,
      };
      moveOps.cutItems([item]);
      showToast(t("move.toasts.cut", { ns: "files", count: 1 }));
    },
    [moveOps, nodeId, showToast, t],
  );

  // Build folder operations adapter
  const folderOperations = buildFolderOperations(
    folderOps,
    goToFolder,
    handleShareFolder,
    handleCutFolder,
  );

  // Build file operations adapter
  const fileOperations = buildFileOperations(fileOps, {
    onDownload: handleDownloadFile,
    onShare: handleShareFile,
    onCut: handleCutFile,
    onClick: handleFileClick,
    onMediaClick: handleMediaClick,
  });

  const handleDeleteSelected = useDeleteSelectedItems({
    nodeId,
    fileSelection,
    tiles,
    confirm,
    t,
    deleteFolder,
    optimisticDeleteFile,
    reloadCurrentNode,
  });

  const isCreatingInThisFolder =
    folderOps.isCreatingFolder && folderOps.newFolderParentId === nodeId;

  const customActionItems = useMemo<
    React.ComponentProps<typeof PageHeader>["customActionItems"]
  >(() => {
    const items: NonNullable<
      React.ComponentProps<typeof PageHeader>["customActionItems"]
    > = [];
    if (fileSelection.selectionMode && fileSelection.selectedCount > 0) {
      items.push({
        key: "cut-selected",
        icon: <ContentCut />,
        title: t("move.cut", { ns: "files" }),
        onClick: handleCutSelection,
        disabled: loading,
      });
      items.push({
        key: "delete-selected",
        icon: <Delete />,
        title: t("selection.deleteSelected", { ns: "files" }),
        onClick: () => {
          void handleDeleteSelected();
        },
        disabled: loading,
        color: "error" as const,
      });
    }
    if (clipboardItems.length > 0 && nodeId) {
      items.push({
        key: "paste-here",
        icon: <ContentPaste />,
        title: t("move.pasteHere", {
          ns: "files",
          count: clipboardItems.length,
        }),
        onClick: handlePasteHere,
        disabled: loading,
      });
    }
    return items.length > 0 ? items : undefined;
  }, [
    clipboardItems.length,
    fileSelection.selectionMode,
    fileSelection.selectedCount,
    handleCutSelection,
    handleDeleteSelected,
    handlePasteHere,
    loading,
    nodeId,
    t,
  ]);

  const pageHeaderProps = useMemo(
    (): React.ComponentProps<typeof PageHeader> => ({
      loading,
      breadcrumbs,
      stats,
      viewMode,
      canGoUp: ancestors.length > 0,
      onGoUp: handleGoUp,
      onHomeClick: goHome,
      onViewModeCycle: cycleViewMode,
      showViewModeToggle: !isHugeFolder,
      showUpload: !!nodeId,
      showNewFolder: !!nodeId,
      onUploadClick: fileUpload.handleUploadClick,
      onNewFolderClick: folderOps.handleNewFolder,
      isCreatingFolder: folderOps.isCreatingFolder,
      selectionMode: fileSelection.selectionMode,
      selectedCount: fileSelection.selectedCount,
      onSelectAll: () => fileSelection.selectAll(tiles),
      onDeselectAll: fileSelection.deselectAll,
      customActionItems,
      breadcrumbsDropHandlers,
      goUpDropHandlers,
    }),
    [
      ancestors.length,
      breadcrumbs,
      breadcrumbsDropHandlers,
      customActionItems,
      cycleViewMode,
      goUpDropHandlers,
      fileSelection,
      fileUpload.handleUploadClick,
      folderOps.handleNewFolder,
      folderOps.isCreatingFolder,
      goHome,
      handleGoUp,
      isHugeFolder,
      loading,
      nodeId,
      stats,
      tiles,
      viewMode,
    ],
  );

  const handleToggleItem = React.useCallback(
    (
      id: string,
      options?: { shiftKey?: boolean; orderedIds?: ReadonlyArray<string> },
    ) => {
      if (!fileSelection.selectionMode) {
        fileSelection.toggleSelectionMode();
      }
      fileSelection.toggleItem(id, options);
    },
    [fileSelection],
  );

  const fileListViewProps = useMemo(
    (): React.ComponentProps<typeof FileListViewFactory> => ({
      layoutType,
      tiles,
      folderOperations,
      fileOperations,
      onNavigateBack: handleGoUp,
      isCreatingFolder: isCreatingInThisFolder,
      tileSize: tilesSize,
      loading:
        layoutType === InterfaceLayoutType.List
          ? !listContent && !listError
          : (!content && !error) || isContentTransitioning,
      loadingTitle: t("loading.title"),
      loadingCaption: t("loading.caption"),
      emptyStateText:
        layoutType === InterfaceLayoutType.Tiles ? t("empty.all") : undefined,
      newFolderName: folderOps.newFolderName,
      onNewFolderNameChange: folderOps.setNewFolderName,
      onConfirmNewFolder: folderOps.handleConfirmNewFolder,
      onCancelNewFolder: folderOps.handleCancelNewFolder,
      folderNamePlaceholder: t("actions.folderNamePlaceholder"),
      fileNamePlaceholder: t("rename.fileNamePlaceholder", {
        ns: "files",
      }),
      selectionMode: fileSelection.selectionMode,
      selectedIds: fileSelection.selectedIds,
      onToggleItem: handleToggleItem,
      moveSupport,
      pagination:
        undefined,
    }),
    [
      content,
      error,
      fileOperations,
      fileSelection.selectionMode,
      fileSelection.selectedIds,
      handleToggleItem,
      handleGoUp,
      folderOperations,
      folderOps.handleCancelNewFolder,
      folderOps.handleConfirmNewFolder,
      folderOps.newFolderName,
      folderOps.setNewFolderName,
      isContentTransitioning,
      isCreatingInThisFolder,
      layoutType,
      listContent,
      listError,
      moveSupport,
      t,
      tiles,
      tilesSize,
    ],
  );

  return (
    <>
      {fileUpload.dropPreparation.active && (
        <Loader
          overlay
          title={getDropPreparationTitle(t, fileUpload.dropPreparation)}
          caption={getDropPreparationCaption(t, fileUpload.dropPreparation)}
        />
      )}

      <DraggingOverlay
        open={fileUpload.isDragging}
        onDragEnter={fileUpload.handleDragEnter}
        onDragOver={fileUpload.handleDragOver}
        onDragLeave={fileUpload.handleDragLeave}
        onDrop={fileUpload.handleDrop}
        label={t("actions.dropFiles")}
      />
      <Box
        width="100%"
        onDragEnter={fileUpload.handleDragEnter}
        onDragOver={fileUpload.handleDragOver}
        onDragLeave={fileUpload.handleDragLeave}
        onDrop={fileUpload.handleDrop}
        sx={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          flex: 1,
          ...(layoutType === InterfaceLayoutType.List && {
            minHeight: 0,
            overflow: "hidden",
          }),
        }}
      >
        <PageHeader
          {...pageHeaderProps}
        />
        {(error || listError) && (
          <Box mb={1} px={1}>
            <Alert severity="error">{error ?? listError}</Alert>
          </Box>
        )}

        <Box
          sx={
            layoutType === InterfaceLayoutType.List
              ? { flex: 1, minHeight: 0, overflow: "hidden", pb: 1 }
              : {}
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
        onSaved={() => {
          if (nodeId) {
            void refreshNodeContent(nodeId);
          }
        }}
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

      <FileConflictDialog
        open={fileUpload.conflictDialog.state.open}
        newName={fileUpload.conflictDialog.state.newName}
        onResolve={fileUpload.conflictDialog.onResolve}
        onExited={fileUpload.conflictDialog.onExited}
      />
    </>
  );
};
