import React, { useEffect, useMemo } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  List,
  ListItem,
  ListItemText,
  Typography,
} from "@mui/material";
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
import { filesApi } from "../../shared/api/filesApi";
import { fetchServerSettings } from "../../shared/api/queries/serverSettings";
import { nodesApi } from "../../shared/api/nodesApi";
import {
  applyDisplayMetaToFile,
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
import { FileVersionsDialog } from "./components/FileVersionsDialog";
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
import { ClientEncryptionUnlockForm } from "../profile/components/ClientEncryptionUnlockForm";
import { downloadArchive } from "@shared/utils/fileHandlers";
import { uploadFileToNode } from "@shared/upload";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";

const HUGE_FOLDER_THRESHOLD = 100_000;
const MARKDOWN_FILE_CONTENT_TYPE = "text/markdown";

const normalizeSiblingName = (name: string): string =>
  name.trim().toLocaleLowerCase();

const buildUniqueSiblingName = (
  baseName: string,
  siblingNames: ReadonlyArray<string>,
): string => {
  const normalizedNames = new Set(siblingNames.map(normalizeSiblingName));
  if (!normalizedNames.has(normalizeSiblingName(baseName))) {
    return baseName;
  }

  const extensionIndex = baseName.lastIndexOf(".");
  const hasExtension = extensionIndex > 0;
  const nameWithoutExtension = hasExtension
    ? baseName.slice(0, extensionIndex)
    : baseName;
  const extension = hasExtension ? baseName.slice(extensionIndex) : "";

  for (let index = 1; index < 1000; index += 1) {
    const candidate = `${nameWithoutExtension} ${index}${extension}`;
    if (!normalizedNames.has(normalizeSiblingName(candidate))) {
      return candidate;
    }
  }

  return `${nameWithoutExtension} ${Date.now()}${extension}`;
};

const isEditableKeyboardTarget = (target: EventTarget | null): boolean => {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  const tagName = target.tagName.toLocaleLowerCase();
  return (
    target.isContentEditable ||
    tagName === "input" ||
    tagName === "textarea" ||
    tagName === "select"
  );
};

const isCreateFolderShortcut = (event: KeyboardEvent): boolean =>
  (event.ctrlKey || event.metaKey) &&
  event.shiftKey &&
  (event.code === "KeyN" || event.key.toLocaleLowerCase() === "n");

type ClientEncryptionFolderAction = "encrypt-existing" | "decrypt-existing";

type ClientEncryptionUnlockPrompt =
  | { kind: "current" }
  | { kind: "open"; folderId: string }
  | { kind: "action"; action: ClientEncryptionFolderAction };

type FolderEncryptionPromptModel = {
  severity: "info" | "warning";
  message: string;
  action: string;
  disabled: boolean;
  onAction: () => void;
};

type ArchiveDownloadRequest = Parameters<typeof downloadArchive>[0];

const tileId = (tile: FileSystemTile): string =>
  tile.kind === "folder" ? tile.node.id : tile.file.id;

const resolveFilesNodeId = (
  routeNodeId: string | undefined,
  rootNodeId: string | null,
): string | null => routeNodeId ?? rootNodeId ?? null;

const getCurrentContent = <TContent,>(
  nodeId: string | null,
  isUserCacheValid: boolean,
  contentByNodeId: Record<string, TContent>,
): TContent | undefined =>
  nodeId && isUserCacheValid ? contentByNodeId[nodeId] : undefined;

const isHugeFolderCount = (childrenTotalCount: number | null): boolean =>
  childrenTotalCount !== null && childrenTotalCount > HUGE_FOLDER_THRESHOLD;

const getActiveCurrentNode = <TNode extends { id: string }>(
  nodeId: string | null,
  currentNode: TNode | null | undefined,
): TNode | null => (nodeId && currentNode?.id === nodeId ? currentNode : null);

const getGoUpParentId = (
  ancestors: ReadonlyArray<{ id: string }>,
): string | null =>
  ancestors.length > 0 ? ancestors[ancestors.length - 1].id : null;

const shouldPromptForCurrentFolderUnlock = (options: {
  clientEncryptionEnabled: boolean;
  currentNodeId?: string | null;
  isVaultUnlocked: boolean;
  nodeId: string | null;
}): boolean =>
  Boolean(options.nodeId && options.currentNodeId === options.nodeId) &&
  !options.isVaultUnlocked &&
  options.clientEncryptionEnabled;

const isFilesUnlockDialogOpen = (
  prompt: ClientEncryptionUnlockPrompt | null,
  envelope: ReturnType<typeof readEnvelopeFromPreferences>,
): boolean => prompt !== null && envelope !== null;

const shouldRenderFilesList = (
  error: string | null,
  content: unknown,
): boolean => !error || Boolean(content);

const buildFolderEncryptionPrompt = (options: {
  decryptEncryptedFiles: () => void;
  encryptedFilesCount: number;
  encryptedFilesMessage: string;
  encryptedFilesAction: string;
  encryptPlainFiles: () => void;
  folderPolicyEnabled: boolean;
  isDecryptingEncryptedFiles: boolean;
  isEncryptingPlainFiles: boolean;
  plainFilesCount: number;
  plainFilesMessage: string;
  plainFilesAction: string;
}): FolderEncryptionPromptModel | null => {
  if (
    options.folderPolicyEnabled &&
    options.plainFilesCount > 0 &&
    !options.isEncryptingPlainFiles
  ) {
    return {
      severity: "warning",
      message: options.plainFilesMessage,
      action: options.plainFilesAction,
      disabled: false,
      onAction: () => {
        void options.encryptPlainFiles();
      },
    };
  }

  if (
    !options.folderPolicyEnabled &&
    options.encryptedFilesCount > 0 &&
    !options.isDecryptingEncryptedFiles
  ) {
    return {
      severity: "info",
      message: options.encryptedFilesMessage,
      action: options.encryptedFilesAction,
      disabled: false,
      onAction: () => {
        void options.decryptEncryptedFiles();
      },
    };
  }

  return null;
};

const buildSelectionArchiveRequest = (
  tiles: ReadonlyArray<FileSystemTile>,
  selectedIds: ReadonlySet<string>,
  currentFolderName?: string | null,
): ArchiveDownloadRequest | null => {
  const selectedTiles = tiles.filter((tile) => selectedIds.has(tileId(tile)));
  if (selectedTiles.length === 0) return null;

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
      : currentFolderName;

  return { fileIds, nodeIds, archiveName: archiveName ?? undefined };
};

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

type FilesPageViewProps = {
  activeUnlockPrompt: ClientEncryptionUnlockPrompt | null;
  clientEncryptionEnvelope: ReturnType<typeof readEnvelopeFromPreferences>;
  closePreview: FileListPageLogic["interaction"]["closePreview"];
  error: string | null;
  fileListViewProps: React.ComponentProps<typeof FileListViewFactory>;
  fileUpload: ReturnType<typeof useFileUpload>;
  folderEncryptionPrompt: FolderEncryptionPromptModel | null;
  getDownloadUrl: FileListPageLogic["interaction"]["getDownloadUrl"];
  getSignedMediaUrl: FileListPageLogic["interaction"]["getSignedMediaUrl"];
  handleCloseVersions: () => void;
  handleLightboxDelete: (
    item: FileListPageLogic["interaction"]["mediaItems"][number],
  ) => Promise<void>;
  handleUnlockCancel: () => void;
  handleUnlockSuccess: () => void;
  handleVersionsChanged: () => void;
  layoutType: InterfaceLayoutType;
  lightboxIndex: number;
  lightboxOpen: boolean;
  mediaItems: FileListPageLogic["interaction"]["mediaItems"];
  pageHeaderProps: React.ComponentProps<typeof PageHeader>;
  previewState: FileListPageLogic["interaction"]["previewState"];
  refreshCurrentNodeContent: () => void;
  setLightboxOpen: FileListPageLogic["interaction"]["setLightboxOpen"];
  shouldRenderFileList: boolean;
  smoothGalleryTransitions: boolean;
  t: ReturnType<typeof useTranslation>["t"];
  unlockDialogOpen: boolean;
  versionDialogFile: { id: string; name: string } | null;
};

export const FilesPage: React.FC = () => {
  const { t } = useTranslation(["files", "common"]);
  const confirm = useConfirm();
  const queryClient = useQueryClient();
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

  useFilesRealtimeEvents({
    nodeId,
    onInvalidate: reloadCurrentNode,
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

  const effectiveContent = content;
  const activeCurrentNode = getActiveCurrentNode(nodeId, currentNode);
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

  const folderOps = useFolderOperations(nodeId, handleFolderChanged);
  const fileUpload = useFileUpload(nodeId, breadcrumbs, content, {
    onToast: showToast,
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
  }, [
    folderOps.isCreatingFolder,
    handleNewFolderClick,
    loading,
    nodeId,
  ]);
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
  const activeUnlockPrompt = useMemo<ClientEncryptionUnlockPrompt | null>(() => {
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
        action === "encrypt-existing" ? encryptPlainFiles : decryptEncryptedFiles;

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
        encryptedFilesMessage: t("clientEncryption.encryptedFilesRemain.toast", {
          ns: "files",
          count: encryptedFiles.length,
        }),
        encryptedFilesAction: t("clientEncryption.encryptedFilesRemain.action", {
          ns: "files",
        }),
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
        plainFilesAction: t("clientEncryption.mixedPlain.action", { ns: "files" }),
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

  const handleOpenVersions = React.useCallback((fileId: string, fileName: string) => {
    setVersionDialogFile({ id: fileId, name: fileName });
  }, []);

  const handleCloseVersions = React.useCallback(() => {
    setVersionDialogFile(null);
  }, []);

  const handleVersionsChanged = React.useCallback(() => {
    if (nodeId) {
      void refreshNodeContent(nodeId);
    }
  }, [nodeId]);

  // Build file operations adapter
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

  const unlockDialogOpen = isFilesUnlockDialogOpen(
    activeUnlockPrompt,
    clientEncryptionEnvelope,
  );
  const shouldRenderFileList = shouldRenderFilesList(error, effectiveContent);

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

const FilesPageView: React.FC<FilesPageViewProps> = ({
  activeUnlockPrompt,
  clientEncryptionEnvelope,
  closePreview,
  error,
  fileListViewProps,
  fileUpload,
  folderEncryptionPrompt,
  getDownloadUrl,
  getSignedMediaUrl,
  handleCloseVersions,
  handleLightboxDelete,
  handleUnlockCancel,
  handleUnlockSuccess,
  handleVersionsChanged,
  layoutType,
  lightboxIndex,
  lightboxOpen,
  mediaItems,
  pageHeaderProps,
  previewState,
  refreshCurrentNodeContent,
  setLightboxOpen,
  shouldRenderFileList,
  smoothGalleryTransitions,
  t,
  unlockDialogOpen,
  versionDialogFile,
}) => (
  <>
    <FilesDropPreparationLoader fileUpload={fileUpload} t={t} />
    <FilesPageContentPanel
      error={error}
      fileListViewProps={fileListViewProps}
      fileUpload={fileUpload}
      layoutType={layoutType}
      pageHeaderProps={pageHeaderProps}
      shouldRenderFileList={shouldRenderFileList}
      t={t}
      unlockDialogOpen={unlockDialogOpen}
    />
    <FilesPreviewLayers
      closePreview={closePreview}
      fileUpload={fileUpload}
      getDownloadUrl={getDownloadUrl}
      getSignedMediaUrl={getSignedMediaUrl}
      handleCloseVersions={handleCloseVersions}
      handleLightboxDelete={handleLightboxDelete}
      handleVersionsChanged={handleVersionsChanged}
      lightboxIndex={lightboxIndex}
      lightboxOpen={lightboxOpen}
      mediaItems={mediaItems}
      previewState={previewState}
      refreshCurrentNodeContent={refreshCurrentNodeContent}
      setLightboxOpen={setLightboxOpen}
      smoothGalleryTransitions={smoothGalleryTransitions}
      versionDialogFile={versionDialogFile}
    />
    <FilesEncryptionPrompts
      activeUnlockPrompt={activeUnlockPrompt}
      clientEncryptionEnvelope={clientEncryptionEnvelope}
      folderEncryptionPrompt={folderEncryptionPrompt}
      handleUnlockCancel={handleUnlockCancel}
      handleUnlockSuccess={handleUnlockSuccess}
      t={t}
      unlockDialogOpen={unlockDialogOpen}
    />
  </>
);

type FilesPageContentPanelProps = Pick<
  FilesPageViewProps,
  | "error"
  | "fileListViewProps"
  | "fileUpload"
  | "layoutType"
  | "pageHeaderProps"
  | "shouldRenderFileList"
  | "t"
  | "unlockDialogOpen"
>;

const FilesDropPreparationLoader: React.FC<
  Pick<FilesPageViewProps, "fileUpload" | "t">
> = ({ fileUpload, t }) =>
  fileUpload.dropPreparation.active ? (
    <Loader
      overlay
      title={getDropPreparationTitle(t, fileUpload.dropPreparation)}
      caption={getDropPreparationCaption(t, fileUpload.dropPreparation)}
    />
  ) : null;

const FilesPageContentPanel: React.FC<FilesPageContentPanelProps> = ({
  error,
  fileListViewProps,
  fileUpload,
  layoutType,
  pageHeaderProps,
  shouldRenderFileList,
  t,
  unlockDialogOpen,
}) => (
  <>
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
      <PageHeader {...pageHeaderProps} />
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
  </>
);

type FilesPreviewLayersProps = Pick<
  FilesPageViewProps,
  | "closePreview"
  | "fileUpload"
  | "getDownloadUrl"
  | "getSignedMediaUrl"
  | "handleCloseVersions"
  | "handleLightboxDelete"
  | "handleVersionsChanged"
  | "lightboxIndex"
  | "lightboxOpen"
  | "mediaItems"
  | "previewState"
  | "refreshCurrentNodeContent"
  | "setLightboxOpen"
  | "smoothGalleryTransitions"
  | "versionDialogFile"
>;

const FilesPreviewLayers: React.FC<FilesPreviewLayersProps> = ({
  closePreview,
  fileUpload,
  getDownloadUrl,
  getSignedMediaUrl,
  handleCloseVersions,
  handleLightboxDelete,
  handleVersionsChanged,
  lightboxIndex,
  lightboxOpen,
  mediaItems,
  previewState,
  refreshCurrentNodeContent,
  setLightboxOpen,
  smoothGalleryTransitions,
  versionDialogFile,
}) => (
  <>
    <FilePreviewModal
      isOpen={previewState.isOpen}
      fileId={previewState.fileId}
      fileName={previewState.fileName}
      fileType={previewState.fileType}
      fileSizeBytes={previewState.fileSizeBytes}
      file={previewState.file}
      onClose={closePreview}
      onSaved={refreshCurrentNodeContent}
    />

    {lightboxOpen && mediaItems.length > 0 && (
      <MediaLightbox
        items={mediaItems}
        open={lightboxOpen}
        initialIndex={lightboxIndex}
        onClose={() => setLightboxOpen(false)}
        getSignedMediaUrl={getSignedMediaUrl}
        getDownloadUrl={getDownloadUrl}
        onDelete={handleLightboxDelete}
        smoothTransitions={smoothGalleryTransitions}
      />
    )}

    <FileConflictDialog
      open={fileUpload.conflictDialog.state.open}
      newName={fileUpload.conflictDialog.state.newName}
      onResolve={fileUpload.conflictDialog.onResolve}
      onExited={fileUpload.conflictDialog.onExited}
    />

    <SkippedUploadItemsDialog
      open={fileUpload.skippedItemsDialog.state.open}
      total={fileUpload.skippedItemsDialog.state.total}
      items={fileUpload.skippedItemsDialog.state.items}
      truncated={fileUpload.skippedItemsDialog.state.truncated}
      onClose={fileUpload.skippedItemsDialog.onClose}
    />

    <FileVersionsDialog
      open={versionDialogFile !== null}
      fileId={versionDialogFile?.id ?? null}
      fileName={versionDialogFile?.name ?? ""}
      onClose={handleCloseVersions}
      onRestored={handleVersionsChanged}
    />
  </>
);

type SkippedUploadItemsDialogProps = {
  open: boolean;
  total: number;
  items: string[];
  truncated: boolean;
  onClose: () => void;
};

const SkippedUploadItemsDialog: React.FC<SkippedUploadItemsDialogProps> = ({
  open,
  total,
  items,
  truncated,
  onClose,
}) => {
  const { t } = useTranslation(["files", "common"]);

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="md">
      <DialogTitle>{t("uploadDrop.skippedDialog.title", { ns: "files" })}</DialogTitle>
      <DialogContent dividers>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          {t("uploadDrop.skippedDialog.description", { ns: "files", count: total })}
        </Typography>

        {items.length > 0 && (
          <List dense disablePadding sx={{ maxHeight: 360, overflow: "auto" }}>
            {items.map((item, index) => (
              <ListItem key={`${item}-${index}`} disableGutters>
                <ListItemText
                  primary={item}
                  primaryTypographyProps={{
                    variant: "body2",
                    sx: { overflowWrap: "anywhere", wordBreak: "break-word" },
                  }}
                />
              </ListItem>
            ))}
          </List>
        )}

        {truncated && (
          <Alert severity="info" sx={{ mt: 2 }}>
            {t("uploadDrop.skippedDialog.truncated", { ns: "files", count: items.length })}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{t("common:actions.close")}</Button>
      </DialogActions>
    </Dialog>
  );
};

type FilesEncryptionPromptsProps = Pick<
  FilesPageViewProps,
  | "activeUnlockPrompt"
  | "clientEncryptionEnvelope"
  | "folderEncryptionPrompt"
  | "handleUnlockCancel"
  | "handleUnlockSuccess"
  | "t"
  | "unlockDialogOpen"
>;

const FilesEncryptionPrompts: React.FC<FilesEncryptionPromptsProps> = ({
  activeUnlockPrompt,
  clientEncryptionEnvelope,
  folderEncryptionPrompt,
  handleUnlockCancel,
  handleUnlockSuccess,
  t,
  unlockDialogOpen,
}) => (
  <>
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
