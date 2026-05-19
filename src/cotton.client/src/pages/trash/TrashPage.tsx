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

export const TrashPage: React.FC = () => {
  const { t } = useTranslation(["trash", "common", "files"]);
  const navigate = useNavigate();
  const params = useParams<{ nodeId?: string }>();
  const confirm = useConfirm();

  const routeNodeId = params.nodeId;
  const isTrashRoot = !routeNodeId;

  const storedLayoutType = useLocalPreferencesStore(selectTrashLayoutType);
  const layoutType = storedLayoutType ?? InterfaceLayoutType.Tiles;
  const tilesSize = useLocalPreferencesStore(selectTrashTilesSize) as TilesSize;
  const setLayoutType = useLocalPreferencesStore((s) => s.setTrashLayoutType);
  const setTilesSize = useLocalPreferencesStore((s) => s.setTrashTilesSize);

  const viewMode = getFileBrowserViewMode(layoutType, tilesSize);

  const cycleViewMode = React.useCallback(() => {
    cycleFileBrowserViewMode(viewMode, setLayoutType, setTilesSize);
  }, [setLayoutType, setTilesSize, viewMode]);

  const queryClient = useQueryClient();
  const rootQuery = useTrashRootQuery(isTrashRoot);
  const nodeId = routeNodeId ?? rootQuery.data?.id ?? null;
  const nodeMetaQuery = useTrashNodeMetaQuery(nodeId, {
    isRoot: isTrashRoot,
    enabled: !!nodeId,
  });
  const currentNode =
    nodeMetaQuery.data?.node ?? (isTrashRoot ? rootQuery.data ?? null : null);
  const ancestors = useMemo(
    () => (isTrashRoot ? [] : (nodeMetaQuery.data?.ancestors ?? [])),
    [isTrashRoot, nodeMetaQuery.data?.ancestors],
  );
  const childrenQuery = useTrashChildrenQuery({
    nodeId,
    isRoot: isTrashRoot,
    enabled: layoutType !== InterfaceLayoutType.List && !!nodeId,
  });
  const content = childrenQuery.data?.content;
  const loading =
    (isTrashRoot && rootQuery.isPending) ||
    (!!nodeId && nodeMetaQuery.isPending) ||
    (layoutType !== InterfaceLayoutType.List &&
      !!nodeId &&
      childrenQuery.isPending);
  const error =
    rootQuery.isError || nodeMetaQuery.isError || childrenQuery.isError
      ? t("error")
      : null;

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

  const pageTitle = useMemo(() => {
    const folderName = currentNode?.name;
    const isRoot = !routeNodeId || ancestors.length === 0;

    if (isRoot) {
      return t("title");
    }

    return folderName ?? null;
  }, [currentNode?.name, routeNodeId, ancestors.length, t]);

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

  const effectiveContent =
    layoutType === InterfaceLayoutType.List
      ? (listContent ?? content)
      : content;

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

  const folderOps = useTrashFolderOperations(
    nodeId,
    refreshContent,
    isTrashRoot ? resolveWrapperNodeId : undefined,
  );
  const fileOps = useTrashFileOperations(
    refreshContent,
    isTrashRoot ? resolveWrapperNodeId : undefined,
  );

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
      onViewModeCycle: cycleViewMode,
      statsNamespace: "trash",
      selectionMode: fileSelection.selectionMode,
      selectedCount: fileSelection.selectedCount,
      onSelectAll: () => fileSelection.selectAll(tiles),
      onDeselectAll: fileSelection.deselectAll,
      customActionItems:
        fileSelection.selectionMode && fileSelection.selectedCount > 0 ? (
          [
            {
              key: "restore-selected-trash",
              icon: <Restore />,
              title: t("restore.action"),
              onClick: () => {
                void restoreSelected();
              },
              disabled: loading || restoring,
              color: "primary" as const,
            },
            {
              key: "delete-selected-trash",
              icon: <Delete />,
              title: t("selection.deleteSelected", { ns: "files" }),
              onClick: () => {
                void handleDeleteSelected();
              },
              disabled: loading || restoring,
              color: "error" as const,
            },
          ]
        ) : breadcrumbs.length <= 1 ? (
          [
            {
              key: "empty-trash",
              icon: <Delete />,
              title: t("actions.emptyTrash"),
              onClick: handleEmptyTrash,
              disabled:
                loading || emptyingTrash || stats.folders + stats.files === 0,
              color: "error" as const,
            },
          ]
        ) : undefined,
    }),
    [
      breadcrumbs,
      navigateToBreadcrumb,
      cycleViewMode,
      emptyingTrash,
      fileSelection,
      goHome,
      handleDeleteSelected,
      handleEmptyTrash,
      handleGoUp,
      layoutType,
      loading,
      restoreSelected,
      restoring,
      stats,
      t,
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
      loading:
        layoutType === InterfaceLayoutType.List
          ? (!listContent && !listLoadError) || listLoading
          : !hasContent && !error,
      loadingTitle: t("loading.title"),
      loadingCaption: t("loading.caption"),
      emptyStateText: layoutType === InterfaceLayoutType.Tiles ? t("empty") : undefined,
      newFolderName: "",
      onNewFolderNameChange: () => {},
      onConfirmNewFolder: () => Promise.resolve(),
      onCancelNewFolder: () => {},
      folderNamePlaceholder: "",
      fileNamePlaceholder: t("rename.fileNamePlaceholder", { ns: "files" }),
      selectionMode: fileSelection.selectionMode,
      selectedIds: fileSelection.selectedIds,
      onToggleItem: handleToggleItem,
      pagination:
        layoutType === InterfaceLayoutType.List
          ? {
              totalCount: listTotalCount,
              loading: listLoading,
              onPaginationModelChange,
            }
          : undefined,
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

  if (!hasContent && !error && layoutType !== InterfaceLayoutType.List) {
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
        {(error || listLoadError) && (
          <Box mb={1} px={1}>
            <Alert severity="error">{error ?? listLoadError}</Alert>
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

      <TrashProgressDialog
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
      <TrashProgressDialog
        open={restoring}
        title={t("restore.inProgress", {
          current: restoreProgress.current,
          total: restoreProgress.total,
          name: restoreProgress.itemName,
        })}
        progressPercent={
          restoreProgress.total > 0
            ? (restoreProgress.current / restoreProgress.total) * 100
            : 0
        }
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
