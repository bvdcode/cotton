import React, { useEffect, useMemo } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "@shared/ui/notifications";
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
import { useFilesLayout } from "@shared/hooks/useFilesLayout";
import { useFilesData } from "./hooks/useFilesData";
import { useFilesRealtimeEvents } from "./hooks/useFilesRealtimeEvents";
import { useFileSelection } from "@shared/hooks/useFileSelection";
import { buildBreadcrumbs } from "./utils/nodeUtils";
import { getFileTypeInfo } from "@shared/utils/fileTypes";
import { invalidateAllFileVersions } from "../../shared/api/queries/fileVersions";
import { useFolderFileList } from "../../shared/hooks/useFileListSource";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { useAudioPlayerStore } from "../../shared/store/audioPlayerStore";
import {
  selectGallerySmoothTransitions,
  useUserPreferencesStore,
} from "../../shared/store/userPreferencesStore";
import { usePageTitle } from "../../shared/hooks/usePageTitle";
import { useFileMoveController } from "./hooks/useFileMoveController";
import { useFileListPageLogic } from "./hooks/useFileListPageLogic";
import { useFilesContentOperations } from "./hooks/useFilesContentOperations";
import { useFilesEncryptionController } from "./hooks/useFilesEncryptionController";
import { useFilesPagePresentation } from "./hooks/useFilesPagePresentation";
import { useFilesSelectionActions } from "./hooks/useFilesSelectionActions";
import { FilesPageView } from "./FilesPageView";
import {
  getActiveCurrentNode,
  getCurrentContent,
  getGoUpParentId,
  isHugeFolderCount,
  resolveFilesNodeId,
  shouldRenderFilesList,
} from "./filesPageModel";

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

  const { sortedFiles, tiles } = fileListLogic;

  const setScanRootNodeId = useAudioPlayerStore((s) => s.setScanRootNodeId);

  useEffect(() => {
    if (!nodeId) return;
    setScanRootNodeId(nodeId);
  }, [nodeId, setScanRootNodeId]);

  const {
    previewState,
    closePreview,
    handleFileClick,
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

  const encryption = useFilesEncryptionController({
    activeCurrentNode,
    ancestors,
    content,
    nodeId,
    showToast,
  });

  const fileSelection = useFileSelection();

  const goUpParentId = getGoUpParentId(ancestors);

  const move = useFileMoveController({
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

  const contentOperations = useFilesContentOperations({
    breadcrumbs,
    content,
    currentFolderEncryptionEnabled:
      encryption.currentFolderEncryptionPolicy.effectiveEnabled,
    ensureCurrentFolderUnlocked: encryption.ensureCurrentFolderUnlocked,
    handleFolderChanged,
    loading,
    nodeId,
    queryClient,
    reloadCurrentNode,
    showToast,
    t,
    tiles,
  });

  const selectionActions = useFilesSelectionActions({
      activeCurrentNode,
      clipboardCount: move.clipboardCount,
      confirm,
      currentFolderName: currentNode?.name,
      fileSelection,
      handleCutSelection: move.handleCutSelection,
      handlePasteHere: move.handlePasteHere,
      loading,
      nodeId,
      optimisticDeleteFile,
      reloadCurrentNode,
      showToast,
      t,
      tiles,
    });

  const presentation = useFilesPagePresentation({
    ancestors,
    breadcrumbs,
    content,
    contentOperations,
    cycleViewMode,
    encryption,
    error,
    fileListLogic,
    fileSelection,
    isHugeFolder,
    layoutType,
    loading,
    move,
    navigate,
    nodeId,
    selectionActions,
    t,
    tiles,
    tilesSize,
    viewMode,
  });

  const shouldRenderFileList = shouldRenderFilesList(error, content);

  return (
    <FilesPageView
      activeUnlockPrompt={encryption.activeUnlockPrompt}
      clientEncryptionEnvelope={encryption.clientEncryptionEnvelope}
      closePreview={closePreview}
      error={error}
      fileListViewProps={presentation.fileListViewProps}
      fileUpload={contentOperations.fileUpload}
      folderEncryptionPrompt={encryption.folderEncryptionPrompt}
      getDownloadUrl={getDownloadUrl}
      getSignedMediaUrl={getSignedMediaUrl}
      handleCloseVersions={presentation.handleCloseVersions}
      handleLightboxDelete={contentOperations.handleLightboxDelete}
      handleUnlockCancel={encryption.handleUnlockCancel}
      handleUnlockSuccess={encryption.handleUnlockSuccess}
      handleVersionsChanged={presentation.handleVersionsChanged}
      layoutType={layoutType}
      lightboxIndex={lightboxIndex}
      lightboxOpen={lightboxOpen}
      mediaItems={mediaItems}
      pageHeaderProps={presentation.pageHeaderProps}
      previewState={previewState}
      refreshCurrentNodeContent={presentation.refreshCurrentNodeContent}
      setLightboxOpen={setLightboxOpen}
      shouldRenderFileList={shouldRenderFileList}
      smoothGalleryTransitions={smoothGalleryTransitions}
      t={t}
      unlockDialogOpen={encryption.unlockDialogOpen}
      versionDialogFile={presentation.versionDialogFile}
    />
  );
};
