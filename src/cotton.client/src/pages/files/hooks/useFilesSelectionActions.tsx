import { useCallback, useMemo } from "react";
import {
  ContentCut,
  ContentPaste,
  Delete,
  Download,
  Share as ShareIcon,
} from "@mui/icons-material";
import type { useConfirm } from "material-ui-confirm";
import type { useTranslation } from "react-i18next";
import type { NodeDto } from "@shared/api/layoutsApi";
import type { FileSelectionState } from "@shared/hooks/useFileSelection";
import type { FileSystemTile } from "@shared/types/FileListViewTypes";
import { downloadArchive } from "@shared/utils/fileHandlers";
import { shareFolder } from "@shared/utils/shareFolder";
import { deleteFolder } from "@shared/store/nodesActions";
import type { PageHeaderProps } from "../components/PageHeader";
import { buildSelectionArchiveRequest } from "../filesPageModel";
import { useDeleteSelectedItems } from "./useDeleteSelectedItems";

interface UseFilesSelectionActionsOptions {
  activeCurrentNode: NodeDto | null;
  clipboardCount: number;
  currentFolderName?: string | null;
  fileSelection: FileSelectionState;
  handleCutSelection: () => void;
  handlePasteHere: () => void;
  loading: boolean;
  nodeId: string | null;
  optimisticDeleteFile: (nodeId: string, fileId: string) => void;
  reloadCurrentNode: () => void;
  showToast: (message: string, variant?: "info" | "error") => void;
  tiles: FileSystemTile[];
  confirm: ReturnType<typeof useConfirm>;
  t: ReturnType<typeof useTranslation>["t"];
}

interface FilesSelectionActions {
  customActionItems: PageHeaderProps["customActionItems"];
  handleDownloadFolder: (
    folderId: string,
    folderName: string,
  ) => Promise<void>;
  handleShareFolder: (
    folderId: string,
    folderName: string,
  ) => Promise<void>;
}

export const useFilesSelectionActions = ({
  activeCurrentNode,
  clipboardCount,
  confirm,
  currentFolderName,
  fileSelection,
  handleCutSelection,
  handlePasteHere,
  loading,
  nodeId,
  optimisticDeleteFile,
  reloadCurrentNode,
  showToast,
  t,
  tiles,
}: UseFilesSelectionActionsOptions): FilesSelectionActions => {
  const handleShareFolder = useCallback(
    async (folderId: string, folderName: string) => {
      await shareFolder(folderId, folderName, t);
    },
    [t],
  );

  const handleDownloadFolder = useCallback(
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

  const handleDownloadSelection = useCallback(async () => {
    const request = buildSelectionArchiveRequest(
      tiles,
      fileSelection.selectedIds,
      currentFolderName,
    );
    if (!request) {
      return;
    }

    try {
      await downloadArchive(request);
      fileSelection.deselectAll();
    } catch {
      showToast(t("selection.downloadFailed", { ns: "files" }), "error");
    }
  }, [currentFolderName, fileSelection, showToast, t, tiles]);

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

  const handleShareCurrentFolder = useCallback(() => {
    if (!activeCurrentNode) {
      return;
    }

    void handleShareFolder(activeCurrentNode.id, activeCurrentNode.name);
  }, [activeCurrentNode, handleShareFolder]);

  const customActionItems = useMemo(() => {
    const items: NonNullable<PageHeaderProps["customActionItems"]> = [];

    if (!fileSelection.selectionMode && activeCurrentNode) {
      items.push({
        key: "share-current-folder",
        icon: <ShareIcon />,
        title: t("actions.share", { ns: "common" }),
        onClick: handleShareCurrentFolder,
        disabled: loading,
      });
    }

    if (fileSelection.selectionMode && fileSelection.selectedCount > 0) {
      items.push(
        {
          key: "download-selected",
          icon: <Download />,
          title: t("selection.downloadSelected", { ns: "files" }),
          onClick: () => {
            void handleDownloadSelection();
          },
          disabled: loading,
        },
        {
          key: "cut-selected",
          icon: <ContentCut />,
          title: t("move.cut", { ns: "files" }),
          onClick: handleCutSelection,
          disabled: loading,
        },
        {
          key: "delete-selected",
          icon: <Delete />,
          title: t("selection.deleteSelected", { ns: "files" }),
          onClick: () => {
            void handleDeleteSelected();
          },
          disabled: loading,
          color: "error",
        },
      );
    }

    if (clipboardCount > 0 && nodeId) {
      items.push({
        key: "paste-here",
        icon: <ContentPaste />,
        title: t("move.pasteHere", {
          ns: "files",
          count: clipboardCount,
        }),
        onClick: handlePasteHere,
        disabled: loading,
      });
    }

    return items.length > 0 ? items : undefined;
  }, [
    activeCurrentNode,
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
  ]);

  return useMemo(
    () => ({
      customActionItems,
      handleDownloadFolder,
      handleShareFolder,
    }),
    [customActionItems, handleDownloadFolder, handleShareFolder],
  );
};
