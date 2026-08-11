import React, { useEffect, useMemo } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Button } from "@mui/material";
import {
  ContentCut,
  ContentPaste,
  Delete,
  Download,
  Share as ShareIcon,
} from "@mui/icons-material";
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
import {
  applyDisplayMetaToFile,
  getFolderEncryptionPolicyState,
  readEnvelopeFromPreferences,
  useVault,
} from "../../shared/crypto";
import { useFolderFileList } from "../../shared/hooks/useFileListSource";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { shareFolder } from "../../shared/utils/shareFolder";
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
import { useFolderClientEncryptionActions } from "./hooks/useFolderClientEncryptionActions";
import { useFolderEncryptionPolicy } from "./hooks/useFolderEncryptionPolicy";
import { downloadArchive } from "@shared/utils/fileHandlers";
import { uploadFileToNode } from "@shared/upload";
import { FilesPageView } from "./FilesPageView";
import {
  buildFolderEncryptionPrompt,
  buildSelectionArchiveRequest,
  buildUniqueSiblingName,
  getActiveCurrentNode,
  getCurrentContent,
  getGoUpParentId,
  isCreateFolderShortcut,
  isEditableKeyboardTarget,
  isFilesUnlockDialogOpen,
  isHugeFolderCount,
  resolveFilesNodeId,
  shouldPromptForCurrentFolderUnlock,
  shouldRenderFilesList,
  type ClientEncryptionFolderAction,
  type ClientEncryptionUnlockPrompt,
} from "./filesPageModel";

const MARKDOWN_FILE_CONTENT_TYPE = "text/markdown";

const buildFilesCustomActionItems = (options: {
  clipboardCount: number;
  cutTitle: string;
  currentFolderId: string | null;
  deleteSelectedTitle: string;
  downloadSelectedTitle: string;
  handleCutSelection: () => void;
  handleDeleteSelected: () => void;
  handleDownloadSelection: () => void;
  handlePasteHere: () => void;
  handleShareCurrentFolder: () => void;
  loading: boolean;
  nodeId: string | null;
  pasteHereTitle: string;
  selectedCount: number;
  selectionMode: boolean;
  shareCurrentFolderTitle: string;
}): React.ComponentProps<typeof PageHeader>["customActionItems"] => {
  const items: NonNullable<
    React.ComponentProps<typeof PageHeader>["customActionItems"]
  > = [];

  if (!options.selectionMode && options.currentFolderId) {
    items.push({
      key: "share-current-folder",
      icon: <ShareIcon />,
      title: options.shareCurrentFolderTitle,
      onClick: options.handleShareCurrentFolder,
      disabled: options.loading,
    });
  }

  if (options.selectionMode && options.selectedCount > 0) {
    items.push({
      key: "download-selected",
      icon: <Download />,
      title: options.downloadSelectedTitle,
      onClick: options.handleDownloadSelection,
      disabled: options.loading,
    });
    items.push({
      key: "cut-selected",
      icon: <ContentCut />,
      title: options.cutTitle,
      onClick: options.handleCutSelection,
      disabled: options.loading,
    });
    items.push({
      key: "delete-selected",
      icon: <Delete />,
      title: options.deleteSelectedTitle,
      onClick: options.handleDeleteSelected,
      disabled: options.loading,
      color: "error" as const,
    });
  }

  if (options.clipboardCount > 0 && options.nodeId) {
    items.push({
      key: "paste-here",
      icon: <ContentPaste />,
      title: options.pasteHereTitle,
      onClick: options.handlePasteHere,
      disabled: options.loading,
    });
  }

  return items.length > 0 ? items : undefined;
};

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
  const activeAncestors = useMemo(
    () => (activeCurrentNode ? ancestors : []),
    [activeCurrentNode, ancestors],
  );
  const currentFolderEncryptionPolicy = useMemo(
    () => getFolderEncryptionPolicyState(activeCurrentNode, activeAncestors),
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
    (folder: NonNullable<typeof content>["nodes"][number]) =>
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
    content,
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
  const { toggleFolderEncryptionPolicy: handleToggleFolderEncryption } =
    useFolderEncryptionPolicy({ onToast: showToast });

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
  const preferences = useUserPreferencesStore((s) => s.preferences);
  const isVaultUnlocked = useVault((state) => state.isUnlocked);
  const clientEncryptionEnvelope = useMemo(
    () => readEnvelopeFromPreferences(preferences),
    [preferences],
  );
  const [unlockPrompt, setUnlockPrompt] =
    React.useState<ClientEncryptionUnlockPrompt | null>(null);
  const currentFolderRequiresUnlock = shouldPromptForCurrentFolderUnlock({
    clientEncryptionEnabled: currentFolderEncryptionPolicy.effectiveEnabled,
    currentNodeId: currentNode?.id,
    isVaultUnlocked,
    nodeId,
  });
  const activeUnlockPrompt =
    useMemo<ClientEncryptionUnlockPrompt | null>(() => {
      if (currentFolderRequiresUnlock && clientEncryptionEnvelope) {
        return { kind: "current" };
      }

      return unlockPrompt;
    }, [clientEncryptionEnvelope, currentFolderRequiresUnlock, unlockPrompt]);

  const handleCreateMarkdownFile = React.useCallback(async () => {
    if (!nodeId || isCreatingMarkdownFile) {
      return;
    }

    if (currentFolderEncryptionPolicy.effectiveEnabled && !isVaultUnlocked) {
      if (!clientEncryptionEnvelope) {
        showToast(
          t("clientEncryption.toasts.setupRequired", { ns: "files" }),
          "error",
        );
        return;
      }

      setUnlockPrompt({ kind: "current" });
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
    clientEncryptionEnvelope,
    currentFolderEncryptionPolicy.effectiveEnabled,
    fileOps,
    getCurrentSiblingNames,
    isCreatingMarkdownFile,
    isVaultUnlocked,
    nodeId,
    queryClient,
    showToast,
    t,
  ]);

  const runFolderClientEncryptionAction = React.useCallback(
    (action: ClientEncryptionFolderAction) => {
      const runAction =
        action === "encrypt-existing"
          ? encryptPlainFiles
          : decryptEncryptedFiles;

      if (isVaultUnlocked) {
        void runAction();
        return;
      }

      if (!clientEncryptionEnvelope) {
        showToast(
          t("clientEncryption.toasts.setupRequired", { ns: "files" }),
          "error",
        );
        return;
      }

      setUnlockPrompt({ kind: "action", action });
    },
    [
      clientEncryptionEnvelope,
      decryptEncryptedFiles,
      encryptPlainFiles,
      isVaultUnlocked,
      showToast,
      t,
    ],
  );

  const folderEncryptionPrompt = useMemo(
    () =>
      buildFolderEncryptionPrompt({
        decryptEncryptedFiles: () =>
          runFolderClientEncryptionAction("decrypt-existing"),
        encryptedFilesCount: encryptedFiles.length,
        encryptedFilesMessage: t(
          "clientEncryption.encryptedFilesRemain.toast",
          {
            ns: "files",
            count: encryptedFiles.length,
          },
        ),
        encryptedFilesAction: t(
          "clientEncryption.encryptedFilesRemain.action",
          {
            ns: "files",
          },
        ),
        encryptPlainFiles: () =>
          runFolderClientEncryptionAction("encrypt-existing"),
        folderPolicyEnabled,
        isDecryptingEncryptedFiles,
        isEncryptingPlainFiles,
        plainFilesCount: plainFiles.length,
        plainFilesMessage: t("clientEncryption.mixedPlain.toast", {
          ns: "files",
          count: plainFiles.length,
        }),
        plainFilesAction: t("clientEncryption.mixedPlain.action", {
          ns: "files",
        }),
      }),
    [
      encryptedFiles.length,
      runFolderClientEncryptionAction,
      folderPolicyEnabled,
      isDecryptingEncryptedFiles,
      isEncryptingPlainFiles,
      plainFiles.length,
      t,
    ],
  );

  const stats = useMemo(
    () => calculateFolderStats(content?.nodes, content?.files),
    [content?.files, content?.nodes],
  );

  const goToFolder = React.useCallback(
    (folderId: string) => {
      const targetFolder = content?.nodes?.find(
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
      content?.nodes,
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

    showToast(
      t("clientEncryption.toasts.setupRequired", { ns: "files" }),
      "error",
    );
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
      return;
    }

    if (prompt?.kind === "action") {
      const runAction =
        prompt.action === "encrypt-existing"
          ? encryptPlainFiles
          : decryptEncryptedFiles;
      void runAction();
    }
  }, [activeUnlockPrompt, decryptEncryptedFiles, encryptPlainFiles, navigate]);

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

  const handleShareCurrentFolder = React.useCallback(() => {
    if (!activeCurrentNode) return;
    void handleShareFolder(activeCurrentNode.id, activeCurrentNode.name);
  }, [activeCurrentNode, handleShareFolder]);

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
    const request = buildSelectionArchiveRequest(
      tiles,
      fileSelection.selectedIds,
      currentNode?.name,
    );
    if (!request) return;

    try {
      await downloadArchive(request);
      fileSelection.deselectAll();
    } catch {
      showToast(t("selection.downloadFailed", { ns: "files" }), "error");
    }
  }, [currentNode?.name, fileSelection, showToast, t, tiles]);

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

  const customActionItems = useMemo(
    () =>
      buildFilesCustomActionItems({
        clipboardCount,
        cutTitle: t("move.cut", { ns: "files" }),
        currentFolderId: activeCurrentNode?.id ?? null,
        deleteSelectedTitle: t("selection.deleteSelected", { ns: "files" }),
        downloadSelectedTitle: t("selection.downloadSelected", { ns: "files" }),
        handleCutSelection,
        handleDeleteSelected: () => {
          void handleDeleteSelected();
        },
        handleDownloadSelection: () => {
          void handleDownloadSelection();
        },
        handlePasteHere,
        handleShareCurrentFolder,
        loading,
        nodeId,
        pasteHereTitle: t("move.pasteHere", {
          ns: "files",
          count: clipboardCount,
        }),
        selectedCount: fileSelection.selectedCount,
        selectionMode: fileSelection.selectionMode,
        shareCurrentFolderTitle: t("actions.share", { ns: "common" }),
      }),
    [
      activeCurrentNode?.id,
      clipboardCount,
      fileSelection.selectedCount,
      fileSelection.selectionMode,
      handleCutSelection,
      handleDeleteSelected,
      handleDownloadSelection,
      handlePasteHere,
      handleShareCurrentFolder,
      loading,
      nodeId,
      t,
    ],
  );

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

  const unlockDialogOpen = isFilesUnlockDialogOpen(
    activeUnlockPrompt,
    clientEncryptionEnvelope,
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
