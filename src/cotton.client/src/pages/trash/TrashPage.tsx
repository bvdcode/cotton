import React, { useEffect, useMemo } from "react";
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
import { Delete } from "@mui/icons-material";
import { useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import Loader from "../../shared/ui/Loader";
import { useTrashStore } from "../../shared/store/trashStore";
import { useTrashFolderOperations } from "./hooks/useTrashFolderOperations";
import { useTrashFileOperations } from "./hooks/useTrashFileOperations";
import { useTrashBulkActions, useTrashListData } from "./hooks";
import {
  buildBreadcrumbs,
  calculateFolderStats,
} from "../files/utils/nodeUtils";
import { useContentTiles } from "../../shared/hooks/useContentTiles";
import { useTrashFileList } from "../../shared/hooks/useFileListSource";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import {
  selectTrashLayoutType,
  selectTrashTilesSize,
  useLocalPreferencesStore,
} from "../../shared/store/localPreferencesStore";
import type { TilesSize } from "../files/types/FileListViewTypes";
import type {
  FileOperations,
  FolderOperations,
} from "../files/types/FileListViewTypes";
import { useFileSelection } from "../files/hooks/useFileSelection";
import {
  cycleFileBrowserViewMode,
  getFileBrowserViewMode,
} from "../files/utils/viewMode";

type EmptyTrashProgressDialogProps = {
  open: boolean;
  title: string;
  progressPercent: number;
};

const EmptyTrashProgressDialog: React.FC<EmptyTrashProgressDialogProps> = ({
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

  const {
    currentNode,
    ancestors,
    contentByNodeId,
    loading,
    error,
    loadRoot,
    loadNode,
    refreshNodeContent,
  } = useTrashStore();

  const routeNodeId = params.nodeId;

  const storedLayoutType = useLocalPreferencesStore(selectTrashLayoutType);
  const layoutType = storedLayoutType ?? InterfaceLayoutType.Tiles;
  const tilesSize = useLocalPreferencesStore(selectTrashTilesSize) as TilesSize;
  const setLayoutType = useLocalPreferencesStore((s) => s.setTrashLayoutType);
  const setTilesSize = useLocalPreferencesStore((s) => s.setTrashTilesSize);

  const viewMode = getFileBrowserViewMode(layoutType, tilesSize);

  const cycleViewMode = React.useCallback(() => {
    cycleFileBrowserViewMode(viewMode, setLayoutType, setTilesSize);
  }, [setLayoutType, setTilesSize, viewMode]);

  useEffect(() => {
    const loadChildren = layoutType !== InterfaceLayoutType.List;
    if (!routeNodeId) {
      void loadRoot({ force: false, loadChildren });
    } else {
      void loadNode(routeNodeId, { loadChildren });
    }
  }, [routeNodeId, layoutType, loadRoot, loadNode]);

  const nodeId = routeNodeId ?? currentNode?.id ?? null;
  const content = nodeId ? contentByNodeId[nodeId] : undefined;

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
    fallbackContent: content,
    loadErrorText: t("error"),
  });
  const refreshContent = React.useCallback(async () => {
    if (!nodeId) return;
    if (layoutType === InterfaceLayoutType.List) {
      reloadListPage();
      return;
    }

    void refreshNodeContent(nodeId);
  }, [layoutType, nodeId, refreshNodeContent, reloadListPage]);

  useEffect(() => {
    const folderName = currentNode?.name;
    const isRoot = !routeNodeId || ancestors.length === 0;

    if (isRoot) {
      document.title = `Cotton - ${t("title")}`;
    } else if (folderName) {
      document.title = `Cotton - ${folderName}`;
    } else {
      document.title = "Cotton";
    }

    return () => {
      document.title = "Cotton";
    };
  }, [currentNode?.name, routeNodeId, ancestors.length, t]);

  const breadcrumbs = useMemo(
    () => buildBreadcrumbs(ancestors, currentNode),
    [ancestors, currentNode],
  );

  const effectiveContent =
    layoutType === InterfaceLayoutType.List
      ? (listContent ?? content)
      : content;

  useTrashFileList({
    nodeId,
    layoutType,
    listContent,
  });

  const { tiles } = useContentTiles(effectiveContent);

  const fileSelection = useFileSelection();

  const isTrashRoot = !routeNodeId;

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
      onDelete: (folderId: string, folderName: string) => {
        void folderOps.handleDeleteFolder(folderId, folderName);
      },
    }),
    [folderOps, goToFolder],
  );

  const fileOperations = React.useMemo<FileOperations>(
    () => ({
      isRenaming: () => false,
      getRenamingName: () => "",
      onRenamingNameChange: () => {},
      onClick: () => {
        // No preview/download in Trash.
      },
      onDelete: (fileId: string, fileName: string) => {
        void fileOps.handleDeleteFile(fileId, fileName);
      },
    }),
    [fileOps],
  );

  const stats = useMemo(
    () => calculateFolderStats(content?.nodes, content?.files),
    [content?.files, content?.nodes],
  );

  const handleGoUp = React.useCallback(() => {
    if (ancestors.length > 0) {
      const parent = ancestors[ancestors.length - 1];
      navigate(`/trash/${parent.id}`);
    } else {
      navigate("/trash");
    }
  }, [ancestors, navigate]);

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
      stats,
      viewMode,
      canGoUp: ancestors.length > 0,
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
              key: "delete-selected-trash",
              icon: <Delete />,
              title: t("selection.deleteSelected", { ns: "files" }),
              onClick: () => {
                void handleDeleteSelected();
              },
              disabled: loading,
              color: "error" as const,
            },
          ]
        ) : ancestors.length === 0 ? (
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
      ancestors.length,
      breadcrumbs,
      cycleViewMode,
      emptyingTrash,
      fileSelection,
      goHome,
      handleDeleteSelected,
      handleEmptyTrash,
      handleGoUp,
      layoutType,
      loading,
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
      isCreatingFolder: isCreatingInThisFolder,
      tileSize: tilesSize,
      loading:
        layoutType === InterfaceLayoutType.List
          ? (!listContent && !listLoadError) || listLoading
          : !content && !error,
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
      content,
      error,
      fileOperations,
      fileSelection.selectedIds,
      fileSelection.selectionMode,
      folderOperations,
      handleToggleItem,
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

  if (!content && !error && layoutType !== InterfaceLayoutType.List) {
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

      <EmptyTrashProgressDialog
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
    </>
  );
};
