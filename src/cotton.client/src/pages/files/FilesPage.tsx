import React, { useEffect, useMemo } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "@shared/ui/notifications";
import { useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useNodesStore } from "../../shared/store/nodesStore";
import {
  loadNode,
  loadRoot,
  resolveRootInBackground,
} from "../../shared/store/nodesActions";
import { useFilesLayout } from "@shared/hooks/useFilesLayout";
import { useFilesData } from "./hooks/useFilesData";
import { useFilesRealtimeEvents } from "./hooks/useFilesRealtimeEvents";
import { useFileSelection } from "@shared/hooks/useFileSelection";
import { buildBreadcrumbs } from "./utils/nodeUtils";
import { invalidateAllFileVersions } from "../../shared/api/queries/fileVersions";
import { useFolderFileList } from "../../shared/hooks/useFileListSource";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { useAudioPlayerStore } from "../../shared/store/audioPlayerStore";
import {
  selectGallerySmoothTransitions,
  useUserPreferencesStore,
} from "../../shared/store/userPreferencesStore";
import { usePageTitle } from "../../shared/hooks/usePageTitle";
import { usePinnedFolders } from "@shared/dashboard/usePinnedFolders";
import { useFileMoveController } from "./hooks/useFileMoveController";
import { useFileListPageLogic } from "./hooks/useFileListPageLogic";
import { useFilesContentOperations } from "./hooks/useFilesContentOperations";
import { useFilesEncryptionController } from "./hooks/useFilesEncryptionController";
import { useFilesSelectionActions } from "./hooks/useFilesSelectionActions";
import { FilesPageContent } from "./components/FilesPageContent";
import { FilesPageHeader } from "./components/FilesPageHeader";
import { FilesPageList } from "./components/FilesPageList";
import {
  FilesDropPreparationLoader,
  FilesPageOverlays,
} from "./components/FilesPageOverlays";
import {
  getActiveCurrentNode,
  getGoUpParentId,
  isHugeFolderCount,
  resolveFilesNodeId,
  shouldRenderFilesList,
} from "./filesPageModel";

export const FilesPage: React.FC = () => {
  const { t } = useTranslation(["files", "common"]);
  const confirm = useConfirm();
  const pinnedFolders = usePinnedFolders();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const params = useParams<{ nodeId?: string }>();

  const {
    currentNode,
    ancestors,
    rootNodeId,
    loading: nodeLoading,
    error: nodeError,
    optimisticDeleteFile,
  } = useNodesStore();

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
      loadChildren: false,
    });
  }, [routeNodeId, layoutType]);

  const nodeId = resolveFilesNodeId(routeNodeId, rootNodeId);
  const {
    content,
    stats,
    loading: contentLoading,
    error: contentError,
    pagination,
    loadMore,
    childrenTotalCount,
    handleFolderChanged,
    reloadCurrentNode,
  } = useFilesData({
    nodeId,
    layoutType,
    loadNode,
  });
  const loading = nodeLoading || contentLoading;
  const error = nodeError ?? contentError;

  const handleRealtimeInvalidate = React.useCallback(() => {
    void invalidateAllFileVersions(queryClient);
    reloadCurrentNode();
  }, [queryClient, reloadCurrentNode]);

  useFilesRealtimeEvents({
    nodeId,
    onInvalidate: handleRealtimeInvalidate,
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
    content,
    loading,
    error,
    refresh: handleFolderChanged,
    deferContent: true,
  });

  const fileListLogic = useFileListPageLogic({
    source: fileListSource,
    sourceKind: "nodes",
  });

  const { tiles } = fileListLogic;

  const setScanRootNodeId = useAudioPlayerStore((s) => s.setScanRootNodeId);

  useEffect(() => {
    if (!nodeId) return;
    setScanRootNodeId(nodeId);
  }, [nodeId, setScanRootNodeId]);

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
    stats,
    nodeId,
    showToast,
  });

  const fileSelection = useFileSelection();
  const { deselectAll } = fileSelection;

  useEffect(() => {
    deselectAll();
  }, [deselectAll, nodeId]);

  const folderPagination = useMemo(
    () =>
      pagination && {
        ...pagination,
        onPaginationModelChange: (model: {
          page: number;
          pageSize: number;
        }) => {
          deselectAll();
          pagination.onPaginationModelChange(model);
        },
      },
    [deselectAll, pagination],
  );

  const handleGoUp = React.useCallback(() => {
    if (ancestors.length === 0) {
      navigate("/files");
      return;
    }

    const parent = ancestors[ancestors.length - 1];
    navigate(`/files/${parent.id}`);
  }, [ancestors, navigate]);

  const goUpParentId = getGoUpParentId(ancestors);

  const move = useFileMoveController({
    nodeId,
    tiles,
    selectedIds: fileSelection.selectedIds,
    selectedCount: fileSelection.selectedCount,
    goUpParentId,
    onClipboardSet: fileSelection.deselectAll,
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
  });

  const selectionActions = useFilesSelectionActions({
    activeCurrentNode,
    clipboardCount: move.clipboardCount,
    confirm,
    currentFolderName: currentNode?.name,
    fileSelection,
    handleCutSelection: move.handleCutSelection,
    handleCopySelection: move.handleCopySelection,
    handlePasteHere: move.handlePasteHere,
    loading,
    nodeId,
    optimisticDeleteFile,
    reloadCurrentNode,
    showToast,
    t,
    tiles,
  });

  const shouldRenderFileList = shouldRenderFilesList(error, content);

  return (
    <>
      <FilesDropPreparationLoader fileUpload={contentOperations.fileUpload} />
      <FilesPageContent
        error={error}
        fileUpload={contentOperations.fileUpload}
        header={
          <FilesPageHeader
            breadcrumbs={breadcrumbs}
            canGoUp={ancestors.length > 0}
            stats={stats}
            contentOperations={contentOperations}
            cycleViewMode={cycleViewMode}
            fileSelection={fileSelection}
            isHugeFolder={isHugeFolder}
            loading={loading}
            move={move}
            nodeId={nodeId}
            pinnedFolders={pinnedFolders}
            onGoHome={encryption.goHome}
            onGoUp={handleGoUp}
            selectionActions={selectionActions}
            tiles={tiles}
            viewMode={viewMode}
          />
        }
        layoutType={layoutType}
        unlockDialogOpen={encryption.unlockDialogOpen}
      >
        <FilesPageList
          content={content}
          contentOperations={contentOperations}
          encryption={encryption}
          error={error}
          fileListLogic={fileListLogic}
          fileSelection={fileSelection}
          layoutType={layoutType}
          pagination={folderPagination}
          onLoadMore={loadMore}
          move={move}
          pinnedFolders={pinnedFolders}
          nodeId={nodeId}
          onNavigateBack={handleGoUp}
          selectionActions={selectionActions}
          shouldRenderFileList={shouldRenderFileList}
          tiles={tiles}
          tilesSize={tilesSize}
        />
      </FilesPageContent>
      <FilesPageOverlays
        encryption={encryption}
        fileUpload={contentOperations.fileUpload}
        handleLightboxDelete={contentOperations.handleLightboxDelete}
        interaction={fileListLogic.interaction}
        move={move}
        nodeId={nodeId}
        smoothGalleryTransitions={smoothGalleryTransitions}
      />
    </>
  );
};
