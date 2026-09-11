import React, { useCallback, useState } from "react";
import { Box } from "@mui/material";
import { useTranslation } from "react-i18next";
import { InterfaceLayoutType } from "@shared/api/layoutsApi";
import type { NodeContentDto } from "@shared/api/nodesApi";
import type { FileSelectionState } from "@shared/hooks/useFileSelection";
import type { useFilesLayout } from "@shared/hooks/useFilesLayout";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";
import {
  buildFileOperations,
  buildFolderOperations,
} from "@shared/utils/operationsAdapters";
import { refreshNodeContent } from "@shared/store/nodesActions";
import type { useFileMoveController } from "../hooks/useFileMoveController";
import type { FileListPageLogic } from "../hooks/useFileListPageLogic";
import type { useFilesContentOperations } from "../hooks/useFilesContentOperations";
import type { useFilesEncryptionController } from "../hooks/useFilesEncryptionController";
import type { useFilesSelectionActions } from "../hooks/useFilesSelectionActions";
import { FileListViewFactory } from "./views";
import { FileVersionsDialog } from "./FileVersionsDialog";
import { usePinnedFolders } from "@shared/dashboard/usePinnedFolders";

interface FilesPageListProps {
  content: NodeContentDto | undefined;
  contentOperations: ReturnType<typeof useFilesContentOperations>;
  encryption: ReturnType<typeof useFilesEncryptionController>;
  error: string | null;
  fileListLogic: FileListPageLogic;
  fileSelection: FileSelectionState;
  layoutType: InterfaceLayoutType;
  move: ReturnType<typeof useFileMoveController>;
  nodeId: string | null;
  onNavigateBack: () => void;
  selectionActions: ReturnType<typeof useFilesSelectionActions>;
  shouldRenderFileList: boolean;
  tiles: FileSystemTile[];
  tilesSize: ReturnType<typeof useFilesLayout>["tilesSize"];
}

export const FilesPageList: React.FC<FilesPageListProps> = ({
  content,
  contentOperations,
  encryption,
  error,
  fileListLogic,
  fileSelection,
  layoutType,
  move,
  nodeId,
  onNavigateBack,
  selectionActions,
  shouldRenderFileList,
  tiles,
  tilesSize,
}) => {
  const { t } = useTranslation("files");
  const [versionDialogFile, setVersionDialogFile] = useState<{
    id: string;
    name: string;
  } | null>(null);
  const pinnedFolders = usePinnedFolders();

  const handleOpenVersions = useCallback((fileId: string, fileName: string) => {
    setVersionDialogFile({ id: fileId, name: fileName });
  }, []);
  const handleCloseVersions = useCallback(() => {
    setVersionDialogFile(null);
  }, []);
  const handleVersionsChanged = useCallback(() => {
    if (nodeId) {
      void refreshNodeContent(nodeId);
    }
  }, [nodeId]);
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

  const folderOperations = buildFolderOperations(
    contentOperations.folderOps,
    encryption.goToFolder,
    selectionActions.handleShareFolder,
    move.handleCutFolder,
    encryption.handleToggleFolderEncryption,
    encryption.getChildFolderEncryptionPolicyState,
    selectionActions.handleDownloadFolder,
    pinnedFolders.togglePinned,
    pinnedFolders.isPinned,
  );
  const fileOperations = buildFileOperations(contentOperations.fileOps, {
    onDownload: fileListLogic.interaction.handleDownloadFile,
    onVersions: handleOpenVersions,
    onShare: fileListLogic.interaction.handleShareFile,
    onCut: move.handleCutFile,
    onClick: fileListLogic.interaction.handleFileClick,
    onMediaClick: fileListLogic.interaction.handleMediaClick,
  });
  const isCreatingInThisFolder =
    contentOperations.folderOps.isCreatingFolder &&
    contentOperations.folderOps.newFolderParentId === nodeId;
  const isLoading =
    layoutType === InterfaceLayoutType.List
      ? !content && !error
      : (!content && !error) || fileListLogic.isContentTransitioning;

  return (
    <>
      {shouldRenderFileList && (
        <Box
          sx={
            layoutType === InterfaceLayoutType.List
              ? { flex: 1, minHeight: 0, overflow: "hidden", pb: 1 }
              : {}
          }
        >
          <FileListViewFactory
            layoutType={layoutType}
            tiles={tiles}
            folderOperations={folderOperations}
            fileOperations={fileOperations}
            onNavigateBack={onNavigateBack}
            isCreatingFolder={isCreatingInThisFolder}
            tileSize={tilesSize}
            loading={isLoading}
            loadingTitle={t("loading.title")}
            loadingCaption={t("loading.caption")}
            emptyStateText={
              !error && layoutType === InterfaceLayoutType.Tiles
                ? t("empty.all")
                : undefined
            }
            newFolderName={contentOperations.folderOps.newFolderName}
            onNewFolderNameChange={contentOperations.folderOps.setNewFolderName}
            onConfirmNewFolder={
              contentOperations.folderOps.handleConfirmNewFolder
            }
            onCancelNewFolder={
              contentOperations.folderOps.handleCancelNewFolder
            }
            folderNamePlaceholder={t("actions.folderNamePlaceholder")}
            fileNamePlaceholder={t("rename.fileNamePlaceholder")}
            selectionMode={fileSelection.selectionMode}
            selectedIds={fileSelection.selectedIds}
            onToggleItem={handleToggleItem}
            moveSupport={move.moveSupport}
          />
        </Box>
      )}

      <FileVersionsDialog
        open={versionDialogFile !== null}
        fileId={versionDialogFile?.id ?? null}
        fileName={versionDialogFile?.name ?? ""}
        onClose={handleCloseVersions}
        onRestored={handleVersionsChanged}
      />
    </>
  );
};
