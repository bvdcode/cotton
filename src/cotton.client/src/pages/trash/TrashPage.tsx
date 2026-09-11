import React, { useCallback, useEffect, useMemo } from "react";
import { useConfirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import { useNavigate, useParams } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import {
  invalidateTrashChildren,
  useTrashChildrenQuery,
  useTrashNodeMetaQuery,
  useTrashRootQuery,
} from "../../shared/api/queries/trash";
import { useFileSelection } from "../../shared/hooks/useFileSelection";
import { useAuth } from "../../features/auth";
import { useTrashFileList } from "../../shared/hooks/useFileListSource";
import { usePageTitle } from "../../shared/hooks/usePageTitle";
import {
  selectTrashLayoutType,
  selectTrashTilesSize,
  useLocalPreferencesStore,
} from "../../shared/store/localPreferencesStore";
import type { TilesSize } from "../../shared/types/FileListViewTypes";
import {
  cycleFileBrowserViewMode,
  getFileBrowserViewMode,
} from "../../shared/utils/viewMode";
import {
  HUB_METHODS,
  useFileTreeRealtimeInvalidation,
  type HubMethodOrLower,
} from "../../shared/signalr";
import { useFileListSourceLogic } from "../files/hooks/useFileListPageLogic";
import { TrashPageContent } from "./components/TrashPageContent";
import { TrashPageHeader } from "./components/TrashPageHeader";
import { TrashPageList } from "./components/TrashPageList";
import {
  useTrashBulkActions,
  useTrashFileOperations,
  useTrashFolderOperations,
  useTrashListData,
  useTrashRestoreActions,
} from "./hooks";
import {
  buildVisibleTrashBreadcrumbs,
  findTrashWrapperNodeId,
  isCurrentTrashWrapper,
} from "./utils/trashBreadcrumbs";

const TRASH_MUTATION_METHODS = new Set<string>(
  [
    HUB_METHODS.FileDeleted,
    HUB_METHODS.FileRestored,
    HUB_METHODS.NodeDeleted,
    HUB_METHODS.NodeRestored,
  ].map((method) => method.toLowerCase()),
);

const shouldInvalidateTrash = (method: HubMethodOrLower): boolean =>
  TRASH_MUTATION_METHODS.has(method.toLowerCase());

export const TrashPage: React.FC = () => {
  const { t } = useTranslation(["trash", "common", "files", "tasks"]);
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const { nodeId: routeNodeId } = useParams<{ nodeId?: string }>();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const isTrashRoot = !routeNodeId;

  const storedLayoutType = useLocalPreferencesStore(selectTrashLayoutType);
  const layoutType = storedLayoutType ?? InterfaceLayoutType.Tiles;
  const tilesSize = useLocalPreferencesStore(selectTrashTilesSize) as TilesSize;
  const setLayoutType = useLocalPreferencesStore(
    (state) => state.setTrashLayoutType,
  );
  const setTilesSize = useLocalPreferencesStore(
    (state) => state.setTrashTilesSize,
  );
  const viewMode = getFileBrowserViewMode(layoutType, tilesSize);

  const rootQuery = useTrashRootQuery(isTrashRoot);
  const nodeId = routeNodeId ?? rootQuery.data?.id ?? null;
  const nodeMetaQuery = useTrashNodeMetaQuery(nodeId, {
    isRoot: isTrashRoot,
    enabled: Boolean(nodeId),
  });
  const currentNode =
    nodeMetaQuery.data?.node ?? (isTrashRoot ? (rootQuery.data ?? null) : null);
  const ancestors = useMemo(
    () => (isTrashRoot ? [] : (nodeMetaQuery.data?.ancestors ?? [])),
    [isTrashRoot, nodeMetaQuery.data?.ancestors],
  );
  const childrenQuery = useTrashChildrenQuery({
    nodeId,
    isRoot: isTrashRoot,
    enabled: layoutType !== InterfaceLayoutType.List && Boolean(nodeId),
  });
  const content = childrenQuery.data?.content;
  const loading =
    (isTrashRoot && rootQuery.isPending) ||
    (Boolean(nodeId) && nodeMetaQuery.isPending) ||
    (layoutType !== InterfaceLayoutType.List &&
      Boolean(nodeId) &&
      childrenQuery.isPending);
  const error =
    rootQuery.isError || nodeMetaQuery.isError || childrenQuery.isError
      ? t("error")
      : null;

  const listData = useTrashListData({
    nodeId,
    routeNodeId,
    layoutType,
    loadErrorText: t("error"),
  });
  const { listContent, listError, reloadListPage } = listData;
  const refreshContent = useCallback(async () => {
    if (!nodeId) {
      return;
    }

    if (layoutType === InterfaceLayoutType.List) {
      reloadListPage();
      return;
    }

    await invalidateTrashChildren(queryClient, nodeId);
  }, [layoutType, nodeId, queryClient, reloadListPage]);
  const handleRealtimeInvalidate = useCallback((): void => {
    void refreshContent();
  }, [refreshContent]);
  useFileTreeRealtimeInvalidation({
    enabled: isAuthenticated && Boolean(nodeId),
    onInvalidate: handleRealtimeInvalidate,
    shouldInvalidate: shouldInvalidateTrash,
  });

  const pageTitle =
    !routeNodeId || ancestors.length === 0
      ? t("title")
      : (currentNode?.name ?? null);
  usePageTitle(pageTitle);

  const breadcrumbs = useMemo(
    () => buildVisibleTrashBreadcrumbs(ancestors, currentNode),
    [ancestors, currentNode],
  );
  const currentNodeIsWrapper = useMemo(
    () => isCurrentTrashWrapper(ancestors, currentNode),
    [ancestors, currentNode],
  );

  useEffect(() => {
    if (routeNodeId && currentNodeIsWrapper) {
      navigate("/trash", { replace: true });
    }
  }, [currentNodeIsWrapper, navigate, routeNodeId]);

  const navigateToBreadcrumb = useCallback(
    (breadcrumbIndex: number) => {
      const target = breadcrumbs[breadcrumbIndex];
      if (!target) {
        return;
      }

      navigate(breadcrumbIndex === 0 ? "/trash" : `/trash/${target.id}`);
    },
    [breadcrumbs, navigate],
  );
  const goHome = useCallback(() => navigate("/trash"), [navigate]);
  const goToFolder = useCallback(
    (folderId: string) => navigate(`/trash/${folderId}`),
    [navigate],
  );
  const goUp = useCallback(() => {
    if (breadcrumbs.length <= 1) {
      goHome();
      return;
    }

    navigateToBreadcrumb(breadcrumbs.length - 2);
  }, [breadcrumbs.length, goHome, navigateToBreadcrumb]);
  const cycleViewMode = useCallback(() => {
    cycleFileBrowserViewMode(viewMode, setLayoutType, setTilesSize);
  }, [setLayoutType, setTilesSize, viewMode]);

  const trashFileListSource = useTrashFileList({
    nodeId,
    isRoot: isTrashRoot,
    layoutType,
    listContent,
  });
  const { hasContent, tiles } = useFileListSourceLogic({
    source: trashFileListSource,
    sourceKind: "trash",
  });
  const fileSelection = useFileSelection();
  const effectiveContent =
    layoutType === InterfaceLayoutType.List
      ? (listContent ?? content)
      : content;
  const resolveWrapperNodeId = useCallback(
    (itemId: string): string | null => {
      if (!isTrashRoot) {
        return null;
      }

      return findTrashWrapperNodeId(effectiveContent, itemId);
    },
    [effectiveContent, isTrashRoot],
  );
  const wrapperResolver = isTrashRoot ? resolveWrapperNodeId : undefined;

  const restore = useTrashRestoreActions({
    fileSelection,
    tiles,
    refreshContent,
  });
  const folderOperations = useTrashFolderOperations(
    nodeId,
    refreshContent,
    wrapperResolver,
  );
  const fileOperations = useTrashFileOperations(
    refreshContent,
    wrapperResolver,
  );
  const bulkActions = useTrashBulkActions({
    t,
    confirm,
    content,
    tiles,
    nodeId,
    isTrashRoot,
    fileSelection,
    resolveWrapperNodeId,
    refreshContent,
  });
  const loadError = error ?? listError;

  return (
    <TrashPageContent
      hasContent={hasContent}
      header={
        <TrashPageHeader
          actionsLoading={loading}
          breadcrumbs={breadcrumbs}
          bulkActions={bulkActions}
          content={content}
          fileSelection={fileSelection}
          loading={layoutType !== InterfaceLayoutType.List && loading}
          onGoHome={goHome}
          onGoUp={goUp}
          onNavigateBreadcrumb={navigateToBreadcrumb}
          onViewModeCycle={cycleViewMode}
          restore={restore}
          tiles={tiles}
          viewMode={viewMode}
        />
      }
      layoutType={layoutType}
      loadError={loadError}
      loading={loading}
      restore={restore}
    >
      {(!loadError || hasContent) && (
        <TrashPageList
          fileOperations={fileOperations}
          fileSelection={fileSelection}
          folderOperations={folderOperations}
          hasContent={hasContent}
          layoutType={layoutType}
          listData={listData}
          loadError={loadError}
          loading={loading}
          onNavigateBack={goUp}
          onOpenFolder={goToFolder}
          restoreItem={restore.restoreItem}
          tiles={tiles}
          tilesSize={tilesSize}
        />
      )}
    </TrashPageContent>
  );
};
