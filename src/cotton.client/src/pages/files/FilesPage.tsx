import React, { useEffect, useMemo } from "react";
import { Alert, Box, Dialog, DialogTitle } from "@mui/material";
import { ContentCut, ContentPaste, Delete, Download } from "@mui/icons-material";
import { toast } from "@shared/ui/notifications";
import {
  FileListViewFactory,
  PageHeader,
  FileConflictDialog,
  DraggingOverlay,
  FolderEncryptionActionPrompt,
} from "./components";
import { FilePreviewModal, MediaLightbox } from "@shared/ui/preview";
import { useNavigate, useParams, useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useNodesStore } from "../../shared/store/nodesStore";
import {
  deleteFolder,
  loadNode,
  loadRoot,
  refreshNodeContent,
  resolveRootInBackground,
} from "../../shared/store/nodesActions";
import { useAuthStore } from "../../shared/store/authStore";
import { useFolderOperations } from "./hooks/useFolderOperations";
import { useFileUpload } from "./hooks/useFileUpload";
import { useFileOperations } from "./hooks/useFileOperations";
import { useFilesLayout } from "@shared/hooks/useFilesLayout";
import { useFilesData } from "./hooks/useFilesData";
import { useFilesRealtimeEvents } from "./hooks/useFilesRealtimeEvents";
import { useFileSelection } from "@shared/hooks/useFileSelection";
import { useDeleteSelectedItems } from "./hooks/useDeleteSelectedItems";
import { buildBreadcrumbs, calculateFolderStats } from "./utils/nodeUtils";
import { getFileTypeInfo } from "@shared/utils/fileTypes";
import {
  getDropPreparationCaption,
  getDropPreparationTitle,
} from "./utils/dropPreparation";
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import { nodesApi } from "../../shared/api/nodesApi";
import {
  FOLDER_ENCRYPTION_POLICY_KEY,
  getFolderEncryptionPolicyState,
  readEnvelopeFromPreferences,
  useVault,
} from "../../shared/crypto";
import { useFolderFileList } from "../../shared/hooks/useFileListSource";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { shareFolder } from "../../shared/utils/shareFolder";
import Loader from "../../shared/ui/Loader";
import { blurredDialogBackdropSlotProps } from "../../shared/ui/dialogBackdrop";
import { useAudioPlayerStore } from "../../shared/store/audioPlayerStore";
import {
  selectGallerySmoothTransitions,
  useUserPreferencesStore,
} from "../../shared/store/userPreferencesStore";
import { usePageTitle } from "../../shared/hooks/usePageTitle";
import { useFileMoveController } from "./hooks/useFileMoveController";
import { useFileListPageLogic } from "./hooks/useFileListPageLogic";
import { useFolderClientEncryptionActions } from "./hooks/useFolderClientEncryptionActions";
import { ClientEncryptionUnlockForm } from "../profile/components/ClientEncryptionUnlockForm";
import { downloadArchive } from "@shared/utils/fileHandlers";

const HUGE_FOLDER_THRESHOLD = 100_000;

type ClientEncryptionUnlockPrompt =
  | { kind: "current" }
  | { kind: "open"; folderId: string };

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
  }, [routeNodeId, rootNodeId]);

  // Always keep root node synced with backend resolver (non-blocking).
  useEffect(() => {
    if (routeNodeId) return;
    resolveRootInBackground({
      loadChildren: layoutType !== InterfaceLayoutType.List,
    });
  }, [routeNodeId, layoutType]);

  const nodeId = routeNodeId ?? rootNodeId ?? null;
  const isUserCacheValid = cacheOwnerUserId === currentUserId;
  const content = nodeId && isUserCacheValid ? contentByNodeId[nodeId] : undefined;

  const {
    childrenTotalCount,
    handleFolderChanged,
    reloadCurrentNode,
    optimisticUpdateCurrentNodeFilePreviewHash,
  } = useFilesData({
    nodeId,
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

  const effectiveContent = content;
  const activeCurrentNode =
    nodeId && currentNode?.id === nodeId ? currentNode : null;
  const activeAncestors = useMemo(
    () => (activeCurrentNode ? ancestors : []),
    [activeCurrentNode, ancestors],
  );
  const currentFolderEncryptionPolicy = useMemo(
    () =>
      getFolderEncryptionPolicyState(activeCurrentNode, activeAncestors),
    [activeAncestors, activeCurrentNode],
  );
  const childFolderEncryptionAncestors = useMemo(
    () =>
      activeCurrentNode
        ? [...activeAncestors, activeCurrentNode]
        : activeAncestors,
    [activeAncestors, activeCurrentNode],
  );
  const getChildFolderEncryptionPolicyState = React.useCallback(
    (folder: NonNullable<typeof effectiveContent>["nodes"][number]) =>
      getFolderEncryptionPolicyState(folder, childFolderEncryptionAncestors),
    [childFolderEncryptionAncestors],
  );

  const fileListSource = useFolderFileList({
    nodeId,
    layoutType,
    deferContent: true,
  });

  const fileListLogic = useFileListPageLogic({
    source: fileListSource,
    sourceKind: "nodes",
  });

  const { isContentTransitioning, sortedFiles, tiles } = fileListLogic;

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
  } = fileListLogic.interaction;

  // Consume selectedFileId from router state (e.g. dashboard → open file)
  React.useEffect(() => {
    const targetId = pendingSelectedFileIdRef.current;
    if (!targetId || sortedFiles.length === 0) return;

    const file = sortedFiles.find((f) => f.id === targetId);
    if (!file) return;

    pendingSelectedFileIdRef.current = null;
    window.history.replaceState({}, "");

    const typeInfo = getFileTypeInfo(file.name, file.contentType ?? null, {
      requiresVideoTranscoding: file.requiresVideoTranscoding ?? false,
    });
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

  const folderEncryptionActions = useFolderClientEncryptionActions({
    nodeId,
    currentNode,
    content: effectiveContent,
    folderPolicyEnabled: currentFolderEncryptionPolicy.effectiveEnabled,
    onToast: showToast,
  });
  const {
    decryptEncryptedFiles,
    encryptPlainFiles,
    encryptedFiles,
    folderPolicyEnabled,
    isDecryptingEncryptedFiles,
    isEncryptingPlainFiles,
    plainFiles,
  } = folderEncryptionActions;

  const folderEncryptionPrompt = useMemo(() => {
    if (
      folderPolicyEnabled &&
      plainFiles.length > 0 &&
      !isEncryptingPlainFiles
    ) {
      return {
        severity: "warning" as const,
        message: t("clientEncryption.mixedPlain.toast", {
          ns: "files",
          count: plainFiles.length,
        }),
        action: t("clientEncryption.mixedPlain.action", { ns: "files" }),
        disabled: false,
        onAction: () => {
          void encryptPlainFiles();
        },
      };
    }

    if (
      !folderPolicyEnabled &&
      encryptedFiles.length > 0 &&
      !isDecryptingEncryptedFiles
    ) {
      return {
        severity: "info" as const,
        message: t("clientEncryption.encryptedFilesRemain.toast", {
          ns: "files",
          count: encryptedFiles.length,
        }),
        action: t("clientEncryption.encryptedFilesRemain.action", {
          ns: "files",
        }),
        disabled: false,
        onAction: () => {
          void decryptEncryptedFiles();
        },
      };
    }

    return null;
  }, [
    decryptEncryptedFiles,
    encryptedFiles.length,
    encryptPlainFiles,
    folderPolicyEnabled,
    isDecryptingEncryptedFiles,
    isEncryptingPlainFiles,
    plainFiles.length,
    t,
  ]);

  const folderOps = useFolderOperations(nodeId, handleFolderChanged);
  const fileUpload = useFileUpload(nodeId, breadcrumbs, content, {
    onToast: showToast,
  });
  const fileOps = useFileOperations(reloadCurrentNode);
  const fileSelection = useFileSelection();

  const goUpParentId = ancestors.length > 0
    ? ancestors[ancestors.length - 1].id
    : null;

  const {
    moveSupport,
    clipboardCount,
    handleCutSelection,
    handlePasteHere,
    handleCutFolder,
    handleCutFile,
    goUpDropHandlers,
    breadcrumbsDropHandlers,
  } = useFileMoveController({
    nodeId,
    tiles,
    selectedIds: fileSelection.selectedIds,
    selectedCount: fileSelection.selectedCount,
    goUpParentId,
    showToast,
    t,
  });

  const smoothGalleryTransitions = useUserPreferencesStore(
    selectGallerySmoothTransitions,
  );
  const preferences = useUserPreferencesStore((s) => s.preferences);
  const isVaultUnlocked = useVault((state) => state.isUnlocked);
  const clientEncryptionEnvelope = useMemo(
    () => readEnvelopeFromPreferences(preferences),
    [preferences],
  );
  const [unlockPrompt, setUnlockPrompt] =
    React.useState<ClientEncryptionUnlockPrompt | null>(null);
  const currentFolderRequiresUnlock =
    Boolean(nodeId && currentNode?.id === nodeId) &&
    !isVaultUnlocked &&
    currentFolderEncryptionPolicy.effectiveEnabled;
  const activeUnlockPrompt = useMemo<ClientEncryptionUnlockPrompt | null>(() => {
    if (currentFolderRequiresUnlock && clientEncryptionEnvelope) {
      return { kind: "current" };
    }

    return unlockPrompt;
  }, [clientEncryptionEnvelope, currentFolderRequiresUnlock, unlockPrompt]);

  const stats = useMemo(
    () => calculateFolderStats(effectiveContent?.nodes, effectiveContent?.files),
    [effectiveContent?.files, effectiveContent?.nodes],
  );

  const goToFolder = React.useCallback(
    (folderId: string) => {
      const targetFolder = effectiveContent?.nodes?.find(
        (folder) => folder.id === folderId,
      );
      const requiresUnlock =
        targetFolder &&
        getFolderEncryptionPolicyState(
          targetFolder,
          childFolderEncryptionAncestors,
        ).effectiveEnabled &&
        !isVaultUnlocked;

      if (requiresUnlock) {
        if (!clientEncryptionEnvelope) {
          showToast(
            t("clientEncryption.toasts.setupRequired", { ns: "files" }),
            "error",
          );
          return;
        }

        setUnlockPrompt({ kind: "open", folderId });
        return;
      }

      navigate(`/files/${folderId}`);
    },
    [
      clientEncryptionEnvelope,
      childFolderEncryptionAncestors,
      effectiveContent?.nodes,
      isVaultUnlocked,
      navigate,
      showToast,
      t,
    ],
  );

  const goHome = React.useCallback(() => navigate("/files"), [navigate]);

  useEffect(() => {
    if (!currentFolderRequiresUnlock || clientEncryptionEnvelope) {
      return;
    }

    showToast(t("clientEncryption.toasts.setupRequired", { ns: "files" }), "error");
    goHome();
  }, [
    clientEncryptionEnvelope,
    currentFolderRequiresUnlock,
    goHome,
    showToast,
    t,
  ]);

  const handleUnlockCancel = React.useCallback(() => {
    const prompt = activeUnlockPrompt;
    setUnlockPrompt(null);

    if (prompt?.kind === "current") {
      goHome();
    }
  }, [activeUnlockPrompt, goHome]);

  const handleUnlockSuccess = React.useCallback(() => {
    const prompt = activeUnlockPrompt;
    setUnlockPrompt(null);

    if (prompt?.kind === "open") {
      navigate(`/files/${prompt.folderId}`);
    }
  }, [activeUnlockPrompt, navigate]);

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

  const handleToggleFolderEncryption = React.useCallback(
    async (folderId: string, currentlyEnabled: boolean) => {
      const nextEnabled = !currentlyEnabled;

      try {
        const updated = await nodesApi.updateNodeMetadata(folderId, {
          [FOLDER_ENCRYPTION_POLICY_KEY]: String(nextEnabled),
        });
        useNodesStore.getState().updateNode(updated);
        showToast(
          nextEnabled
            ? t("clientEncryption.toasts.policyEnabled", { ns: "files" })
            : t("clientEncryption.toasts.policyDisabled", { ns: "files" }),
        );
      } catch {
        showToast(
          t("clientEncryption.toasts.policyToggleFailed", { ns: "files" }),
          "error",
        );
      }
    },
    [showToast, t],
  );

  const handleDownloadFolder = React.useCallback(
    async (folderId: string, folderName: string) => {
      try {
        await downloadArchive({
          fileIds: [],
          nodeIds: [folderId],
          archiveName: folderName,
        });
      } catch {
        showToast(t("selection.downloadFailed", { ns: "files" }), "error");
      }
    },
    [showToast, t],
  );

  const handleDownloadSelection = React.useCallback(async () => {
    const selectedTiles = tiles.filter((tile) => {
      const id = tile.kind === "folder" ? tile.node.id : tile.file.id;
      return fileSelection.selectedIds.has(id);
    });

    if (selectedTiles.length === 0) {
      return;
    }

    const fileIds = selectedTiles.flatMap((tile) =>
      tile.kind === "file" ? [tile.file.id] : [],
    );
    const nodeIds = selectedTiles.flatMap((tile) =>
      tile.kind === "folder" ? [tile.node.id] : [],
    );
    const archiveName =
      selectedTiles.length === 1
        ? selectedTiles[0].kind === "folder"
          ? selectedTiles[0].node.name
          : selectedTiles[0].file.name
        : currentNode?.name;

    try {
      await downloadArchive({ fileIds, nodeIds, archiveName });
      fileSelection.deselectAll();
    } catch {
      showToast(t("selection.downloadFailed", { ns: "files" }), "error");
    }
  }, [
    currentNode?.name,
    fileSelection,
    showToast,
    t,
    tiles,
  ]);

  // Build folder operations adapter
  const folderOperations = buildFolderOperations(
    folderOps,
    goToFolder,
    handleShareFolder,
    handleCutFolder,
    handleToggleFolderEncryption,
    getChildFolderEncryptionPolicyState,
    handleDownloadFolder,
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
        key: "download-selected",
        icon: <Download />,
        title: t("selection.downloadSelected", { ns: "files" }),
        onClick: () => {
          void handleDownloadSelection();
        },
        disabled: loading,
      });
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
    if (clipboardCount > 0 && nodeId) {
      items.push({
        key: "paste-here",
        icon: <ContentPaste />,
        title: t("move.pasteHere", {
          ns: "files",
          count: clipboardCount,
        }),
        onClick: handlePasteHere,
        disabled: loading,
      });
    }
    return items.length > 0 ? items : undefined;
  }, [
    clipboardCount,
    fileSelection.selectionMode,
    fileSelection.selectedCount,
    handleCutSelection,
    handleDeleteSelected,
    handleDownloadSelection,
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
          ? !content && !error
          : (!content && !error) || isContentTransitioning,
      loadingTitle: t("loading.title"),
      loadingCaption: t("loading.caption"),
      emptyStateText:
        !error && layoutType === InterfaceLayoutType.Tiles
          ? t("empty.all")
          : undefined,
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
      moveSupport,
      t,
      tiles,
      tilesSize,
    ],
  );

  const unlockDialogOpen =
    activeUnlockPrompt !== null && clientEncryptionEnvelope !== null;
  const shouldRenderFileList = !error || Boolean(effectiveContent);

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
          ...(unlockDialogOpen && {
            filter: "blur(4px)",
            pointerEvents: "none",
            transition: "filter 160ms ease",
            userSelect: "none",
          }),
        }}
      >
        <PageHeader
          {...pageHeaderProps}
        />
        {error && (
          <Box mb={1} px={1}>
            <Alert severity="error">{error}</Alert>
          </Box>
        )}
        {shouldRenderFileList && (
          <Box
            sx={
              layoutType === InterfaceLayoutType.List
                ? { flex: 1, minHeight: 0, overflow: "hidden", pb: 1 }
                : {}
            }
          >
            <FileListViewFactory {...fileListViewProps} />
          </Box>
        )}

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

      {folderEncryptionPrompt && (
        <FolderEncryptionActionPrompt
          action={folderEncryptionPrompt.action}
          disabled={folderEncryptionPrompt.disabled}
          message={folderEncryptionPrompt.message}
          onAction={folderEncryptionPrompt.onAction}
          severity={folderEncryptionPrompt.severity}
        />
      )}

      <Dialog
        open={unlockDialogOpen}
        onClose={handleUnlockCancel}
        fullWidth
        maxWidth="sm"
        slotProps={blurredDialogBackdropSlotProps}
      >
        <DialogTitle>
          {activeUnlockPrompt?.kind === "current"
            ? t("clientEncryption.unlockDialog.currentTitle", { ns: "files" })
            : t("clientEncryption.unlockDialog.title", { ns: "files" })}
        </DialogTitle>
        {clientEncryptionEnvelope && (
          <ClientEncryptionUnlockForm
            envelope={clientEncryptionEnvelope}
            onCancel={handleUnlockCancel}
            onSuccess={handleUnlockSuccess}
            cancelLabel={
              activeUnlockPrompt?.kind === "current"
                ? t("clientEncryption.unlockDialog.goHome", { ns: "files" })
                : undefined
            }
          />
        )}
      </Dialog>
    </>
  );
};
