import React, { useMemo } from "react";
import {
  Alert,
  Box,
  LinearProgress,
  Dialog,
  DialogContent,
  DialogTitle,
} from "@mui/material";
import {
  FileListViewFactory,
  PageHeader,
} from "../files/components";
import { Delete, Restore } from "@mui/icons-material";
import { useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import Loader from "../../shared/ui/Loader";
import { useQueryClient } from "@tanstack/react-query";
import {
  invalidateTrashChildren,
  useTrashChildrenQuery,
  useTrashNodeMetaQuery,
  useTrashRootQuery,
} from "../../shared/api/queries/trash";
import { useTrashFolderOperations } from "./hooks/useTrashFolderOperations";
import { useTrashFileOperations } from "./hooks/useTrashFileOperations";
import { useTrashBulkActions, useTrashListData, useTrashRestoreActions } from "./hooks";
import { RestoreConflictDialog } from "./components/RestoreConflictDialog";
import { calculateFolderStats } from "../files/utils/nodeUtils";
import { useTrashFileList } from "../../shared/hooks/useFileListSource";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import {
  selectTrashLayoutType,
  selectTrashTilesSize,
  useLocalPreferencesStore,
} from "../../shared/store/localPreferencesStore";
import type { TilesSize } from "@shared/types/FileListViewTypes";
import type {
  FileOperations,
  FolderOperations,
} from "@shared/types/FileListViewTypes";
import { useFileSelection } from "@shared/hooks/useFileSelection";
import {
  cycleFileBrowserViewMode,
  getFileBrowserViewMode,
} from "@shared/utils/viewMode";
import { usePageTitle } from "../../shared/hooks/usePageTitle";
import { useFileListSourceLogic } from "../files/hooks/useFileListPageLogic";
import {
  buildVisibleTrashBreadcrumbs,
  isCurrentTrashWrapper,
} from "./utils/trashBreadcrumbs";

type EmptyTrashProgressDialogProps = {
  open: boolean;
  title: string;
  progressPercent: number;
};

type TrashPageViewProps = {
  clearRestoreErrors: () => void;
  emptyingTrash: boolean;
  emptyTrashProgress: ReturnType<typeof useTrashBulkActions>["emptyTrashProgress"];
  fileListViewProps: React.ComponentProps<typeof FileListViewFactory>;
  handlePromptAnswer: ReturnType<typeof useTrashRestoreActions>["handlePromptAnswer"];
  hasContent: boolean;
  layoutType: InterfaceLayoutType;
  loadError: string | null;
  loading: boolean;
  pageHeaderProps: React.ComponentProps<typeof PageHeader>;
  restoreErrors: string[];
  restoreProgress: ReturnType<typeof useTrashRestoreActions>["progress"];
  restorePrompt: ReturnType<typeof useTrashRestoreActions>["activePrompt"];
  restoring: boolean;
  shouldRenderFileList: boolean;
  t: ReturnType<typeof useTranslation>["t"];
};

const TrashProgressDialog: React.FC<EmptyTrashProgressDialogProps> = ({
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

const calculateProgressPercent = (current: number, total: number): number =>
  total > 0 ? (current / total) * 100 : 0;

const isTrashRootRoute = (routeNodeId: string | undefined): boolean =>
  !routeNodeId;

const resolveTrashLayoutType = (
  storedLayoutType: InterfaceLayoutType | null,
): InterfaceLayoutType => storedLayoutType ?? InterfaceLayoutType.Tiles;

const resolveTrashNodeId = (
  routeNodeId: string | undefined,
  rootNodeId?: string | null,
): string | null => routeNodeId ?? rootNodeId ?? null;

const resolveTrashCurrentNode = <TNode,>(
  node: TNode | null | undefined,
  root: TNode | null | undefined,
  isRoot: boolean,
): TNode | null => node ?? (isRoot ? root ?? null : null);

const resolveTrashAncestors = <TAncestor,>(
  isRoot: boolean,
  ancestors: TAncestor[] | null | undefined,
): TAncestor[] => (isRoot ? [] : (ancestors ?? []));

const shouldLoadTrashChildren = (
  layoutType: InterfaceLayoutType,
  nodeId: string | null,
): boolean => layoutType !== InterfaceLayoutType.List && Boolean(nodeId);

const resolveTrashLoading = (options: {
  childrenPending: boolean;
  isRoot: boolean;
  layoutType: InterfaceLayoutType;
  nodeId: string | null;
  nodePending: boolean;
  rootPending: boolean;
}): boolean =>
  (options.isRoot && options.rootPending) ||
  (Boolean(options.nodeId) && options.nodePending) ||
  (shouldLoadTrashChildren(options.layoutType, options.nodeId) &&
    options.childrenPending);

const resolveTrashError = (options: {
  childrenError: boolean;
  errorText: string;
  nodeError: boolean;
  rootError: boolean;
}): string | null =>
  options.rootError || options.nodeError || options.childrenError
    ? options.errorText
    : null;

const resolveEffectiveTrashContent = <TContent,>(
  layoutType: InterfaceLayoutType,
  listContent: TContent | null | undefined,
  content: TContent | null | undefined,
): TContent | null | undefined =>
  layoutType === InterfaceLayoutType.List ? listContent ?? content : content;

type TrashWrapperContent = {
  nodes?: Array<{ id: string; parentId?: string | null }>;
  files?: Array<{ id: string; nodeId?: string | null }>;
};

const resolveTrashWrapperNodeId = (
  isRoot: boolean,
  effectiveContent: TrashWrapperContent | null | undefined,
  itemId: string,
): string | null => {
  if (!isRoot || !effectiveContent) return null;

  const node = effectiveContent.nodes?.find((n) => n.id === itemId);
  if (node?.parentId) return node.parentId;

  const file = effectiveContent.files?.find((f) => f.id === itemId);
  return file?.nodeId ?? null;
};

const getOptionalWrapperResolver = (
  isRoot: boolean,
  resolveWrapperNodeId: (itemId: string) => string | null,
) => (isRoot ? resolveWrapperNodeId : undefined);

const resolveTrashLoadError = (
  error: string | null,
  listLoadError: string | null,
): string | null => error ?? listLoadError;

const shouldRenderTrashFileList = (
  loadError: string | null,
  hasContent: boolean,
): boolean => !loadError || hasContent;

const shouldShowTrashInitialLoader = (options: {
  error: string | null;
  hasContent: boolean;
  layoutType: InterfaceLayoutType;
  loading: boolean;
}): boolean =>
  options.loading &&
  !options.hasContent &&
  !options.error &&
  options.layoutType !== InterfaceLayoutType.List;

const getTrashFileListLoading = (options: {
  error: string | null;
  hasContent: boolean;
  layoutType: InterfaceLayoutType;
  listContent: unknown;
  listLoadError: string | null;
  listLoading: boolean;
  loading: boolean;
}): boolean =>
  options.layoutType === InterfaceLayoutType.List
    ? (!options.listContent && !options.listLoadError) || options.listLoading
    : options.loading && !options.hasContent && !options.error;

const getTrashEmptyStateText = (options: {
  emptyText: string;
  error: string | null;
  layoutType: InterfaceLayoutType;
  listLoadError: string | null;
}): string | undefined =>
  !options.error &&
  !options.listLoadError &&
  options.layoutType === InterfaceLayoutType.Tiles
    ? options.emptyText
    : undefined;

const getTrashPagination = (options: {
  layoutType: InterfaceLayoutType;
  listLoading: boolean;
  listTotalCount: number;
  onPaginationModelChange: (model: { page: number; pageSize: number }) => void;
}): React.ComponentProps<typeof FileListViewFactory>["pagination"] =>
  options.layoutType === InterfaceLayoutType.List
    ? {
        totalCount: options.listTotalCount,
        loading: options.listLoading,
        onPaginationModelChange: options.onPaginationModelChange,
      }
    : undefined;

const resolveTrashPageTitle = (options: {
  ancestorsLength: number;
  folderName?: string | null;
  isRootRoute: boolean;
  title: string;
}): string | null => {
  if (options.isRootRoute || options.ancestorsLength === 0) {
    return options.title;
  }

  return options.folderName ?? null;
};

const buildTrashCustomActionItems = (options: {
  deleteSelectedTitle: string;
  emptyTrashTitle: string;
  emptyingTrash: boolean;
  handleDeleteSelected: () => void;
  handleEmptyTrash: () => void;
  loading: boolean;
  restoreSelected: () => void;
  restoreSelectedTitle: string;
  restoring: boolean;
  rootLevel: boolean;
  selectedCount: number;
  selectionMode: boolean;
  totalItems: number;
}): React.ComponentProps<typeof PageHeader>["customActionItems"] => {
  if (options.selectionMode && options.selectedCount > 0) {
    return [
      {
        key: "restore-selected-trash",
        icon: <Restore />,
        title: options.restoreSelectedTitle,
        onClick: options.restoreSelected,
        disabled: options.loading || options.restoring,
        color: "primary" as const,
      },
      {
        key: "delete-selected-trash",
        icon: <Delete />,
        title: options.deleteSelectedTitle,
        onClick: options.handleDeleteSelected,
        disabled: options.loading || options.restoring,
        color: "error" as const,
      },
    ];
  }

  if (!options.rootLevel) {
    return undefined;
  }

  return [
    {
      key: "empty-trash",
      icon: <Delete />,
      title: options.emptyTrashTitle,
      onClick: options.handleEmptyTrash,
      disabled:
        options.loading || options.emptyingTrash || options.totalItems === 0,
      color: "error" as const,
    },
  ];
};

export const TrashPage: React.FC = () => {
  const { t } = useTranslation(["trash", "common", "files"]);
  const navigate = useNavigate();
  const params = useParams<{ nodeId?: string }>();
  const confirm = useConfirm();

  const routeNodeId = params.nodeId;
  const isTrashRoot = isTrashRootRoute(routeNodeId);

  const storedLayoutType = useLocalPreferencesStore(selectTrashLayoutType);
  const layoutType = resolveTrashLayoutType(storedLayoutType);
  const tilesSize = useLocalPreferencesStore(selectTrashTilesSize) as TilesSize;
  const setLayoutType = useLocalPreferencesStore((s) => s.setTrashLayoutType);
  const setTilesSize = useLocalPreferencesStore((s) => s.setTrashTilesSize);

  const viewMode = getFileBrowserViewMode(layoutType, tilesSize);

  const queryClient = useQueryClient();
  const rootQuery = useTrashRootQuery(isTrashRoot);
  const nodeId = resolveTrashNodeId(routeNodeId, rootQuery.data?.id);
  const nodeMetaQuery = useTrashNodeMetaQuery(nodeId, {
    isRoot: isTrashRoot,
    enabled: !!nodeId,
  });
  const currentNode = resolveTrashCurrentNode(
    nodeMetaQuery.data?.node,
    rootQuery.data,
    isTrashRoot,
  );
  const ancestors = useMemo(
    () => resolveTrashAncestors(isTrashRoot, nodeMetaQuery.data?.ancestors),
    [isTrashRoot, nodeMetaQuery.data?.ancestors],
  );
  const childrenQuery = useTrashChildrenQuery({
    nodeId,
    isRoot: isTrashRoot,
    enabled: shouldLoadTrashChildren(layoutType, nodeId),
  });
  const content = childrenQuery.data?.content;
  const loading = resolveTrashLoading({
    childrenPending: childrenQuery.isPending,
    isRoot: isTrashRoot,
    layoutType,
    nodeId,
    nodePending: nodeMetaQuery.isPending,
    rootPending: rootQuery.isPending,
  });
  const error = resolveTrashError({
    childrenError: childrenQuery.isError,
    errorText: t("error"),
    nodeError: nodeMetaQuery.isError,
    rootError: rootQuery.isError,
  });

  const {
    listTotalCount,
    listLoading,
    listError: listLoadError,
    listContent,
    handlePaginationChange,
    reloadListPage,
  } = useTrashListData({
    nodeId,
    routeNodeId,
    layoutType,
    loadErrorText: t("error"),
  });
  const refreshContent = React.useCallback(async () => {
    if (!nodeId) return;
    if (layoutType === InterfaceLayoutType.List) {
      reloadListPage();
      return;
    }

    await invalidateTrashChildren(queryClient, nodeId);
  }, [layoutType, nodeId, queryClient, reloadListPage]);

  const pageTitle = useMemo(
    () =>
      resolveTrashPageTitle({
        ancestorsLength: ancestors.length,
        folderName: currentNode?.name,
        isRootRoute: !routeNodeId,
        title: t("title"),
      }),
    [ancestors.length, currentNode?.name, routeNodeId, t],
  );

  usePageTitle(pageTitle);

  const breadcrumbs = useMemo(
    () => buildVisibleTrashBreadcrumbs(ancestors, currentNode),
    [ancestors, currentNode],
  );
  const currentNodeIsWrapper = useMemo(
    () => isCurrentTrashWrapper(ancestors, currentNode),
    [ancestors, currentNode],
  );

  React.useEffect(() => {
    if (routeNodeId && currentNodeIsWrapper) {
      navigate("/trash", { replace: true });
    }
  }, [currentNodeIsWrapper, navigate, routeNodeId]);

  const navigateToBreadcrumb = React.useCallback(
    (breadcrumbIndex: number) => {
      const target = breadcrumbs[breadcrumbIndex];
      if (!target) {
        return;
      }

      if (breadcrumbIndex === 0) {
        navigate("/trash");
        return;
      }

      navigate(`/trash/${target.id}`);
    },
    [breadcrumbs, navigate],
  );

  const effectiveContent = resolveEffectiveTrashContent(
    layoutType,
    listContent,
    content,
  );

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

  const goToFolder = React.useMemo(
    () => (folderId: string) => navigate(`/trash/${folderId}`),
    [navigate],
  );

  const goHome = React.useMemo(() => () => navigate("/trash"), [navigate]);

  const resolveWrapperNodeId = React.useCallback(
    (itemId: string): string | null =>
      resolveTrashWrapperNodeId(isTrashRoot, effectiveContent, itemId),
    [isTrashRoot, effectiveContent],
  );

  const {
    restoring,
    progress: restoreProgress,
    errors: restoreErrors,
    activePrompt: restorePrompt,
    handlePromptAnswer,
    restoreItem,
    restoreSelected,
    clearErrors: clearRestoreErrors,
  } = useTrashRestoreActions({
    fileSelection,
    tiles,
    refreshContent,
  });

  const optionalWrapperResolver = getOptionalWrapperResolver(
    isTrashRoot,
    resolveWrapperNodeId,
  );

  const folderOps = useTrashFolderOperations(
    nodeId,
    refreshContent,
    optionalWrapperResolver,
  );
  const fileOps = useTrashFileOperations(refreshContent, optionalWrapperResolver);

  const folderOperations = React.useMemo<FolderOperations>(
    () => ({
      isRenaming: () => false,
      getRenamingName: () => "",
      onRenamingNameChange: () => {},
      onClick: goToFolder,
      onRestore: (folderId: string, folderName: string) => {
        void restoreItem({ id: folderId, kind: "folder", name: folderName });
      },
      onDelete: (folderId: string, folderName: string) => {
        void folderOps.handleDeleteFolder(folderId, folderName);
      },
    }),
    [folderOps, goToFolder, restoreItem],
  );

  const fileOperations = React.useMemo<FileOperations>(
    () => ({
      isRenaming: () => false,
      getRenamingName: () => "",
      onRenamingNameChange: () => {},
      onClick: () => {
        // No preview/download in Trash.
      },
      onRestore: (fileId: string, fileName: string) => {
        void restoreItem({ id: fileId, kind: "file", name: fileName });
      },
      onDelete: (fileId: string, fileName: string) => {
        void fileOps.handleDeleteFile(fileId, fileName);
      },
    }),
    [fileOps, restoreItem],
  );

  const stats = useMemo(
    () => calculateFolderStats(content?.nodes, content?.files),
    [content?.files, content?.nodes],
  );

  const handleGoUp = React.useCallback(() => {
    if (breadcrumbs.length <= 1) {
      navigate("/trash");
      return;
    }

    navigateToBreadcrumb(breadcrumbs.length - 2);
  }, [breadcrumbs.length, navigate, navigateToBreadcrumb]);

  const {
    emptyingTrash,
    emptyTrashProgress,
    handleEmptyTrash,
    handleDeleteSelected,
  } = useTrashBulkActions({
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


  const customActionItems = useMemo(
    () =>
      buildTrashCustomActionItems({
        deleteSelectedTitle: t("selection.deleteSelected", { ns: "files" }),
        emptyTrashTitle: t("actions.emptyTrash"),
        emptyingTrash,
        handleDeleteSelected: () => {
          void handleDeleteSelected();
        },
        handleEmptyTrash,
        loading,
        restoreSelected: () => {
          void restoreSelected();
        },
        restoreSelectedTitle: t("restore.action"),
        restoring,
        rootLevel: breadcrumbs.length <= 1,
        selectedCount: fileSelection.selectedCount,
        selectionMode: fileSelection.selectionMode,
        totalItems: stats.folders + stats.files,
      }),
    [
      breadcrumbs.length,
      emptyingTrash,
      fileSelection.selectedCount,
      fileSelection.selectionMode,
      handleDeleteSelected,
      handleEmptyTrash,
      loading,
      restoreSelected,
      restoring,
      stats.files,
      stats.folders,
      t,
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

  const isCreatingInThisFolder = false;

  const pageHeaderProps = useMemo(
    (): React.ComponentProps<typeof PageHeader> => ({
      loading: layoutType !== InterfaceLayoutType.List && loading,
      breadcrumbs,
      onNavigateBreadcrumb: navigateToBreadcrumb,
      stats,
      viewMode,
      canGoUp: breadcrumbs.length > 1,
      onGoUp: handleGoUp,
      onHomeClick: goHome,
      onViewModeCycle: () => {
        cycleFileBrowserViewMode(viewMode, setLayoutType, setTilesSize);
      },
      statsNamespace: "trash",
      selectionMode: fileSelection.selectionMode,
      selectedCount: fileSelection.selectedCount,
      onSelectAll: () => fileSelection.selectAll(tiles),
      onDeselectAll: fileSelection.deselectAll,
      customActionItems,
    }),
    [
      breadcrumbs,
      navigateToBreadcrumb,
      customActionItems,
      fileSelection,
      goHome,
      handleGoUp,
      layoutType,
      loading,
      stats,
      setLayoutType,
      setTilesSize,
      tiles,
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
      onNavigateBack: handleGoUp,
      isCreatingFolder: isCreatingInThisFolder,
      tileSize: tilesSize,
      loading: getTrashFileListLoading({
        error,
        hasContent,
        layoutType,
        listContent,
        listLoadError,
        listLoading,
        loading,
      }),
      loadingTitle: t("loading.title"),
      loadingCaption: t("loading.caption"),
      emptyStateText: getTrashEmptyStateText({
        emptyText: t("empty"),
        error,
        layoutType,
        listLoadError,
      }),
      newFolderName: "",
      onNewFolderNameChange: () => {},
      onConfirmNewFolder: () => Promise.resolve(),
      onCancelNewFolder: () => {},
      folderNamePlaceholder: "",
      fileNamePlaceholder: t("rename.fileNamePlaceholder", { ns: "files" }),
      selectionMode: fileSelection.selectionMode,
      selectedIds: fileSelection.selectedIds,
      onToggleItem: handleToggleItem,
      pagination: getTrashPagination({
        layoutType,
        listLoading,
        listTotalCount,
        onPaginationModelChange,
      }),
    }),
    [
      error,
      fileOperations,
      fileSelection.selectedIds,
      fileSelection.selectionMode,
      folderOperations,
      handleToggleItem,
      handleGoUp,
      hasContent,
      isCreatingInThisFolder,
      layoutType,
      loading,
      listContent,
      listLoadError,
      listLoading,
      listTotalCount,
      onPaginationModelChange,
      t,
      tiles,
      tilesSize,
    ],
  );

  const loadError = resolveTrashLoadError(error, listLoadError);
  const shouldRenderFileList = shouldRenderTrashFileList(loadError, hasContent);

  return (
    <TrashPageView
      clearRestoreErrors={clearRestoreErrors}
      emptyingTrash={emptyingTrash}
      emptyTrashProgress={emptyTrashProgress}
      fileListViewProps={fileListViewProps}
      handlePromptAnswer={handlePromptAnswer}
      hasContent={hasContent}
      layoutType={layoutType}
      loadError={loadError}
      loading={loading}
      pageHeaderProps={pageHeaderProps}
      restoreErrors={restoreErrors}
      restoreProgress={restoreProgress}
      restorePrompt={restorePrompt}
      restoring={restoring}
      shouldRenderFileList={shouldRenderFileList}
      t={t}
    />
  );
};

const TrashPageView: React.FC<TrashPageViewProps> = ({
  clearRestoreErrors,
  emptyingTrash,
  emptyTrashProgress,
  fileListViewProps,
  handlePromptAnswer,
  hasContent,
  layoutType,
  loadError,
  loading,
  pageHeaderProps,
  restoreErrors,
  restoreProgress,
  restorePrompt,
  restoring,
  shouldRenderFileList,
  t,
}) => {
  if (shouldShowTrashInitialLoader({
    error: loadError,
    hasContent,
    layoutType,
    loading,
  })) {
    return <Loader title={t("loading.title")} caption={t("loading.caption")} />;
  }

  return (
    <>
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
        {loadError && (
          <Box mb={1} px={1}>
            <Alert severity="error">{loadError}</Alert>
          </Box>
        )}
        {restoreErrors.length > 0 && (
          <Box mb={1} px={1}>
            <Alert severity="warning" onClose={clearRestoreErrors}>
              <Box sx={{ whiteSpace: "pre-line" }}>
                {restoreErrors.join("\n")}
              </Box>
            </Alert>
          </Box>
        )}

        {shouldRenderFileList && (
          <Box
            sx={
              layoutType === InterfaceLayoutType.List
                ? { flex: 1, minHeight: 0, overflow: "hidden", pb: 1 }
                : { pb: { xs: 1, sm: 2 } }
            }
          >
            <FileListViewFactory {...fileListViewProps} />
          </Box>
        )}
      </Box>
      <TrashProgressDialog
        open={emptyingTrash}
        title={t("emptyTrash.inProgress", {
          current: emptyTrashProgress.current,
          total: emptyTrashProgress.total,
        })}
        progressPercent={calculateProgressPercent(
          emptyTrashProgress.current,
          emptyTrashProgress.total,
        )}
      />
      <TrashProgressDialog
        open={restoring}
        title={t("restore.inProgress", {
          current: restoreProgress.current,
          total: restoreProgress.total,
          name: restoreProgress.itemName,
        })}
        progressPercent={calculateProgressPercent(
          restoreProgress.current,
          restoreProgress.total,
        )}
      />
      <RestoreConflictDialog
        open={!!restorePrompt}
        itemName={restorePrompt?.item.name ?? ""}
        prompt={restorePrompt?.prompt ?? null}
        showApplyToAll={restoreProgress.total > 1}
        onAnswer={handlePromptAnswer}
      />
    </>
  );
};
