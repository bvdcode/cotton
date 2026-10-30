import React, { useCallback, useMemo } from "react";
import { Box } from "@mui/material";
import { useTranslation } from "react-i18next";
import { InterfaceLayoutType } from "@shared/api/layoutsApi";
import type { FileSelectionState } from "@shared/hooks/useFileSelection";
import type {
  FileOperations,
  FileSystemTile,
  FolderOperations,
  TilesSize,
} from "@shared/types/FileListViewTypes";
import type { useTrashFileOperations } from "../hooks/useTrashFileOperations";
import type { useTrashFolderOperations } from "../hooks/useTrashFolderOperations";
import type { useTrashListData } from "../hooks/useTrashListData";
import type { RestorableItem } from "../hooks/useTrashRestoreActions";
import { FileListViewFactory } from "../../files/components";

interface TrashPageListProps {
  fileOperations: ReturnType<typeof useTrashFileOperations>;
  fileSelection: FileSelectionState;
  folderOperations: ReturnType<typeof useTrashFolderOperations>;
  hasContent: boolean;
  layoutType: InterfaceLayoutType;
  listData: ReturnType<typeof useTrashListData>;
  loadError: string | null;
  loading: boolean;
  onNavigateBack: () => void;
  onOpenFolder: (folderId: string) => void;
  restoreItem: (item: RestorableItem) => Promise<void>;
  tiles: FileSystemTile[];
  tilesSize: TilesSize;
}

export const TrashPageList: React.FC<TrashPageListProps> = ({
  fileOperations,
  fileSelection,
  folderOperations,
  hasContent,
  layoutType,
  listData,
  loadError,
  loading,
  onNavigateBack,
  onOpenFolder,
  restoreItem,
  tiles,
  tilesSize,
}) => {
  const { t } = useTranslation(["trash", "files"]);
  const { handleDeleteFile } = fileOperations;
  const { handleDeleteFolder } = folderOperations;
  const {
    handlePaginationChange,
    listContent,
    listError,
    listLoading,
    listTotalCount,
  } = listData;
  const folderOperationAdapter = useMemo<FolderOperations>(
    () => ({
      isRenaming: () => false,
      getRenamingName: () => "",
      onRenamingNameChange: () => {},
      onClick: onOpenFolder,
      onRestore: (folderId, folderName) => {
        void restoreItem({ id: folderId, kind: "folder", name: folderName });
      },
      onDelete: (folderId, folderName) => {
        void handleDeleteFolder(folderId, folderName);
      },
    }),
    [handleDeleteFolder, onOpenFolder, restoreItem],
  );
  const fileOperationAdapter = useMemo<FileOperations>(
    () => ({
      isRenaming: () => false,
      getRenamingName: () => "",
      onRenamingNameChange: () => {},
      onClick: () => {},
      onRestore: (fileId, fileName) => {
        void restoreItem({ id: fileId, kind: "file", name: fileName });
      },
      onDelete: (fileId, fileName) => {
        void handleDeleteFile(fileId, fileName);
      },
    }),
    [handleDeleteFile, restoreItem],
  );
  const fileListLoading =
    layoutType === InterfaceLayoutType.List
      ? (!listContent && !listError) || listLoading
      : loading && !hasContent && !loadError;
  const handlePaginationModelChange = useCallback(
    (model: { page: number; pageSize: number }) => {
      handlePaginationChange(model.page, model.pageSize);
    },
    [handlePaginationChange],
  );
  const pagination = useMemo(
    () =>
      layoutType === InterfaceLayoutType.List
        ? {
            totalCount: listTotalCount,
            loading: listLoading,
            onPaginationModelChange: handlePaginationModelChange,
          }
        : undefined,
    [handlePaginationModelChange, layoutType, listLoading, listTotalCount],
  );
  const handleToggleItem = useCallback(
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

  return (
    <Box
      sx={
        layoutType === InterfaceLayoutType.List
          ? { flex: 1, minHeight: 0, overflow: "hidden", pb: 1 }
          : { pb: { xs: 1, sm: 2 } }
      }
    >
      <FileListViewFactory
        layoutType={layoutType}
        tiles={tiles}
        folderOperations={folderOperationAdapter}
        fileOperations={fileOperationAdapter}
        onNavigateBack={onNavigateBack}
        isCreatingFolder={false}
        tileSize={tilesSize}
        loading={fileListLoading}
        loadingTitle={t("loading.title")}
        loadingCaption={t("loading.caption")}
        emptyStateText={
          !loadError && layoutType === InterfaceLayoutType.Tiles
            ? t("empty")
            : undefined
        }
        newFolderName=""
        onNewFolderNameChange={() => {}}
        onConfirmNewFolder={() => Promise.resolve()}
        onCancelNewFolder={() => {}}
        folderNamePlaceholder=""
        fileNamePlaceholder={t("rename.fileNamePlaceholder", { ns: "files" })}
        selectionMode={fileSelection.selectionMode}
        selectedIds={fileSelection.selectedIds}
        onToggleItem={handleToggleItem}
        pagination={pagination}
      />
    </Box>
  );
};
