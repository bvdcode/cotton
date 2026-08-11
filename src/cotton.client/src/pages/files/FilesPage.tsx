import React, { useEffect, useMemo } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Button } from "@mui/material";
import { toast } from "@shared/ui/notifications";
import {
  FileListViewFactory,
  PageHeader,
} from "./components";
import { useNavigate, useParams, useLocation } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useNodesStore } from "../../shared/store/nodesStore";
import {
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
import { buildBreadcrumbs, calculateFolderStats } from "./utils/nodeUtils";
import { getFileTypeInfo } from "@shared/utils/fileTypes";
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import { filesApi } from "../../shared/api/filesApi";
import {
  invalidateAllFileVersions,
  invalidateFileVersions,
} from "../../shared/api/queries/fileVersions";
import { fetchServerSettings } from "../../shared/api/queries/serverSettings";
import { type NodeFileManifestDto } from "../../shared/api/nodesApi";
import { applyDisplayMetaToFile } from "../../shared/crypto";
import { useFolderFileList } from "../../shared/hooks/useFileListSource";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { useAudioPlayerStore } from "../../shared/store/audioPlayerStore";
import {
  selectGallerySmoothTransitions,
  useUserPreferencesStore,
} from "../../shared/store/userPreferencesStore";
import { usePageTitle } from "../../shared/hooks/usePageTitle";
import { useFileMoveController } from "./hooks/useFileMoveController";
import {
  useFileListPageLogic,
  type FileListPageLogic,
} from "./hooks/useFileListPageLogic";
import { uploadFileToNode } from "@shared/upload";
import { useFilesEncryptionController } from "./hooks/useFilesEncryptionController";
import { useFilesSelectionActions } from "./hooks/useFilesSelectionActions";
import { FilesPageView } from "./FilesPageView";
import {
  buildUniqueSiblingName,
  getActiveCurrentNode,
  getCurrentContent,
  getGoUpParentId,
  isCreateFolderShortcut,
  isEditableKeyboardTarget,
  isHugeFolderCount,
  resolveFilesNodeId,
  shouldRenderFilesList,
} from "./filesPageModel";

const MARKDOWN_FILE_CONTENT_TYPE = "text/markdown";

export const FilesPage: React.FC = () => {
  const { t } = useTranslation(["files", "common"]);
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();
  const params = useParams<{ nodeId?: string }>();
  const pendingSelectedFileIdRef = React.useRef<string | null>(
    (location.state as { selectedFileId?: string } | null)?.selectedFileId ??
      null,
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

  const nodeId = resolveFilesNodeId(routeNodeId, rootNodeId);
  const isUserCacheValid = cacheOwnerUserId === currentUserId;
  const content = getCurrentContent(nodeId, isUserCacheValid, contentByNodeId);

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

  const handleRealtimeInvalidate = React.useCallback(() => {
    void invalidateAllFileVersions(queryClient);
    reloadCurrentNode();
  }, [queryClient, reloadCurrentNode]);

  useFilesRealtimeEvents({
    nodeId,
    onInvalidate: handleRealtimeInvalidate,
    onPreviewGenerated: optimisticUpdateCurrentNodeFilePreviewHash,
  });

  const isHugeFolder = isHugeFolderCount(childrenTotalCount);

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

  const activeCurrentNode = getActiveCurrentNode(nodeId, currentNode);
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

  const {
    activeUnlockPrompt,
    clientEncryptionEnvelope,
    currentFolderEncryptionPolicy,
    ensureCurrentFolderUnlocked,
    folderEncryptionPrompt,
    getChildFolderEncryptionPolicyState,
    goHome,
    goToFolder,
    handleToggleFolderEncryption,
    handleUnlockCancel,
    handleUnlockSuccess,
    unlockDialogOpen,
  } = useFilesEncryptionController({
    activeCurrentNode,
    ancestors,
    content,
    nodeId,
    showToast,
  });

  const folderOps = useFolderOperations(nodeId, handleFolderChanged);
  const handleFileUploaded = React.useCallback(
    (file: NodeFileManifestDto) => {
      void invalidateFileVersions(queryClient, file.id);
    },
    [queryClient],
  );
  const fileUpload = useFileUpload(nodeId, breadcrumbs, content, {
    onToast: showToast,
    onFileUploaded: handleFileUploaded,
  });
  const fileOps = useFileOperations(reloadCurrentNode);
  const [isCreatingMarkdownFile, setIsCreatingMarkdownFile] =
    React.useState(false);

  const getCurrentSiblingNames = React.useCallback(
    () =>
      tiles.map((tile) =>
        tile.kind === "folder" ? tile.node.name : tile.file.name,
      ),
    [tiles],
  );

  const handleNewFolderClick = React.useCallback(() => {
    const folderName = buildUniqueSiblingName(
      t("actions.defaultNewFolderName", { ns: "files" }),
      getCurrentSiblingNames(),
    );
    folderOps.handleNewFolder(folderName);
  }, [folderOps, getCurrentSiblingNames, t]);

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (
        !nodeId ||
        loading ||
        folderOps.isCreatingFolder ||
        !isCreateFolderShortcut(event) ||
        isEditableKeyboardTarget(event.target)
      ) {
        return;
      }

      event.preventDefault();
      handleNewFolderClick();
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [folderOps.isCreatingFolder, handleNewFolderClick, loading, nodeId]);
  const handleRestoreLightboxFile = React.useCallback(
    async (fileId: string) => {
      try {
        const outcome = await filesApi.restoreFile(fileId);
        if (outcome.status !== "Restored") {
          toast.error(t("preview.deleteUndoFailed", { ns: "files" }));
          return;
        }

        if (nodeId) {
          await refreshNodeContent(nodeId);
        } else {
          reloadCurrentNode();
        }
      } catch (error) {
        console.error("Failed to undo media delete:", error);
        toast.error(t("preview.deleteUndoFailed", { ns: "files" }));
      }
    },
    [nodeId, reloadCurrentNode, t],
  );
  const handleLightboxDelete = React.useCallback(
    async (item: FileListPageLogic["interaction"]["mediaItems"][number]) => {
      await fileOps.deleteFile(item.id);
      toast.info(t("preview.deleteToast", { ns: "files" }), {
        action: (key) => (
          <Button
            color="inherit"
            size="small"
            onClick={() => {
              toast.dismiss(key);
              void handleRestoreLightboxFile(item.id);
            }}
          >
            {t("common:actions.undo")}
          </Button>
        ),
      });
    },
    [fileOps, handleRestoreLightboxFile, t],
  );
  const fileSelection = useFileSelection();
  const [versionDialogFile, setVersionDialogFile] = React.useState<{
    id: string;
    name: string;
  } | null>(null);

  const goUpParentId = getGoUpParentId(ancestors);

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
    onItemsCut: fileSelection.deselectAll,
    showToast,
    t,
  });

  const smoothGalleryTransitions = useUserPreferencesStore(
    selectGallerySmoothTransitions,
  );

  const handleCreateMarkdownFile = React.useCallback(async () => {
    if (!nodeId || isCreatingMarkdownFile) {
      return;
    }

    if (!ensureCurrentFolderUnlocked()) {
      return;
    }

    const fileName = buildUniqueSiblingName(
      t("actions.defaultMarkdownFileName", { ns: "files" }),
      getCurrentSiblingNames(),
    );

    setIsCreatingMarkdownFile(true);

    try {
      const settings = await fetchServerSettings(queryClient);
      const createdFile = await uploadFileToNode({
        file: new File([""], fileName, { type: MARKDOWN_FILE_CONTENT_TYPE }),
        nodeId,
        server: {
          maxChunkSizeBytes: settings.maxChunkSizeBytes,
          supportedHashAlgorithm: settings.supportedHashAlgorithm,
        },
        encrypt: currentFolderEncryptionPolicy.effectiveEnabled,
      });
      const displayFile = await applyDisplayMetaToFile(createdFile);

      useNodesStore.getState().moveFileInCache(displayFile, nodeId, nodeId);
      fileOps.handleRenameFile(displayFile.id, displayFile.name);
      void refreshNodeContent(nodeId);
    } catch (error) {
      console.error("Failed to create markdown file:", error);
      showToast(
        t("uploadDrop.errors.createMarkdownFileFailed", { ns: "files" }),
        "error",
      );
    } finally {
      setIsCreatingMarkdownFile(false);
    }
  }, [
    currentFolderEncryptionPolicy.effectiveEnabled,
    ensureCurrentFolderUnlocked,
    fileOps,
    getCurrentSiblingNames,
    isCreatingMarkdownFile,
    nodeId,
    queryClient,
    showToast,
    t,
  ]);

  const stats = useMemo(
    () => calculateFolderStats(content?.nodes, content?.files),
    [content?.files, content?.nodes],
  );

  const handleGoUp = React.useCallback(() => {
    if (ancestors.length > 0) {
      const parent = ancestors[ancestors.length - 1];
      navigate(`/files/${parent.id}`);
    } else {
      navigate("/files");
    }
  }, [ancestors, navigate]);

  const { customActionItems, handleDownloadFolder, handleShareFolder } =
    useFilesSelectionActions({
      activeCurrentNode,
      clipboardCount,
      confirm,
      currentFolderName: currentNode?.name,
      fileSelection,
      handleCutSelection,
      handlePasteHere,
      loading,
      nodeId,
      optimisticDeleteFile,
      reloadCurrentNode,
      showToast,
      t,
      tiles,
    });

  const folderOperations = buildFolderOperations(
    folderOps,
    goToFolder,
    handleShareFolder,
    handleCutFolder,
    handleToggleFolderEncryption,
    getChildFolderEncryptionPolicyState,
    handleDownloadFolder,
  );

  const handleOpenVersions = React.useCallback(
    (fileId: string, fileName: string) => {
      setVersionDialogFile({ id: fileId, name: fileName });
    },
    [],
  );

  const handleCloseVersions = React.useCallback(() => {
    setVersionDialogFile(null);
  }, []);

  const handleVersionsChanged = React.useCallback(() => {
    if (nodeId) {
      void refreshNodeContent(nodeId);
    }
  }, [nodeId]);

  const fileOperations = buildFileOperations(fileOps, {
    onDownload: handleDownloadFile,
    onVersions: handleOpenVersions,
    onShare: handleShareFile,
    onCut: handleCutFile,
    onClick: handleFileClick,
    onMediaClick: handleMediaClick,
  });

  const isCreatingInThisFolder =
    folderOps.isCreatingFolder && folderOps.newFolderParentId === nodeId;

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
      showNewFile: !!nodeId,
      showNewFolder: !!nodeId,
      onUploadClick: fileUpload.handleUploadClick,
      onNewFileClick: handleCreateMarkdownFile,
      onNewFolderClick: handleNewFolderClick,
      isCreatingFile: isCreatingMarkdownFile,
      isCreatingFolder: folderOps.isCreatingFolder,
      selectionMode: fileSelection.selectionMode,
      selectedCount: fileSelection.selectedCount,
      onToggleSelectionMode: fileSelection.toggleSelectionMode,
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
      handleCreateMarkdownFile,
      handleNewFolderClick,
      isCreatingMarkdownFile,
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

  const shouldRenderFileList = shouldRenderFilesList(error, content);

  const refreshCurrentNodeContent = React.useCallback(() => {
    if (nodeId) {
      void refreshNodeContent(nodeId);
    }
  }, [nodeId]);

  return (
    <FilesPageView
      activeUnlockPrompt={activeUnlockPrompt}
      clientEncryptionEnvelope={clientEncryptionEnvelope}
      closePreview={closePreview}
      error={error}
      fileListViewProps={fileListViewProps}
      fileUpload={fileUpload}
      folderEncryptionPrompt={folderEncryptionPrompt}
      getDownloadUrl={getDownloadUrl}
      getSignedMediaUrl={getSignedMediaUrl}
      handleCloseVersions={handleCloseVersions}
      handleLightboxDelete={handleLightboxDelete}
      handleUnlockCancel={handleUnlockCancel}
      handleUnlockSuccess={handleUnlockSuccess}
      handleVersionsChanged={handleVersionsChanged}
      layoutType={layoutType}
      lightboxIndex={lightboxIndex}
      lightboxOpen={lightboxOpen}
      mediaItems={mediaItems}
      pageHeaderProps={pageHeaderProps}
      previewState={previewState}
      refreshCurrentNodeContent={refreshCurrentNodeContent}
      setLightboxOpen={setLightboxOpen}
      shouldRenderFileList={shouldRenderFileList}
      smoothGalleryTransitions={smoothGalleryTransitions}
      t={t}
      unlockDialogOpen={unlockDialogOpen}
      versionDialogFile={versionDialogFile}
    />
  );
};
