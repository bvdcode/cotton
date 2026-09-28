import { reportClientError } from "@shared/utils/clientDiagnostics";
import { useCallback, useEffect, useState } from "react";
import { Button } from "@mui/material";
import type { QueryClient } from "@tanstack/react-query";
import type { useTranslation } from "react-i18next";
import { toast } from "@shared/ui/notifications";
import { filesApi } from "@shared/api/filesApi";
import { invalidateFileVersions } from "@shared/api/queries/fileVersions";
import { fetchServerSettings } from "@shared/api/queries/serverSettings";
import type {
  NodeContentDto,
  NodeFileManifestDto,
} from "@shared/api/nodesApi";
import { nodesApi } from "@shared/api/nodesApi";
import { applyDisplayMetaToFile } from "@shared/crypto";
import { refreshNodeContent } from "@shared/store/nodesActions";
import { useNodesStore } from "@shared/store/nodesStore";
import { uploadFileToNode } from "@shared/upload";
import {
  buildUniqueSiblingName,
  isCreateFolderShortcut,
  isEditableKeyboardTarget,
} from "../filesPageModel";
import type { FileListPageLogic } from "./useFileListPageLogic";
import { useFileOperations } from "./useFileOperations";
import { useFileUpload } from "./useFileUpload";
import { useFolderOperations } from "./useFolderOperations";

const MARKDOWN_FILE_CONTENT_TYPE = "text/markdown";

interface UseFilesContentOperationsOptions {
  breadcrumbs: Parameters<typeof useFileUpload>[1];
  content: NodeContentDto | undefined;
  currentFolderEncryptionEnabled: boolean;
  ensureCurrentFolderUnlocked: () => boolean;
  handleFolderChanged: () => void;
  loading: boolean;
  nodeId: string | null;
  queryClient: QueryClient;
  reloadCurrentNode: () => void;
  showToast: (message: string, variant?: "info" | "error") => void;
  t: ReturnType<typeof useTranslation>["t"];
}

export const useFilesContentOperations = ({
  breadcrumbs,
  content,
  currentFolderEncryptionEnabled,
  ensureCurrentFolderUnlocked,
  handleFolderChanged,
  loading,
  nodeId,
  queryClient,
  reloadCurrentNode,
  showToast,
  t,
}: UseFilesContentOperationsOptions) => {
  const folderOps = useFolderOperations(nodeId, handleFolderChanged);
  const fileOps = useFileOperations(reloadCurrentNode, content);
  const [isCreatingMarkdownFile, setIsCreatingMarkdownFile] = useState(false);

  const handleFileUploaded = useCallback(
    (file: NodeFileManifestDto) => {
      void invalidateFileVersions(queryClient, file.id);
    },
    [queryClient],
  );
  const fileUpload = useFileUpload(nodeId, breadcrumbs, {
    onToast: showToast,
    onFileUploaded: handleFileUploaded,
  });

  const getAvailableSiblingName = useCallback(
    async (baseName: string): Promise<string> => {
      if (!nodeId) {
        return baseName;
      }
      const lookup = await nodesApi.lookupSiblingNames(nodeId, [baseName], true);
      return buildUniqueSiblingName(baseName, lookup.takenNameKeys);
    },
    [nodeId],
  );

  const handleNewFolderClick = useCallback(async () => {
    if (!nodeId) {
      return;
    }
    try {
      const folderName = await getAvailableSiblingName(
        t("actions.defaultNewFolderName", { ns: "files" }),
      );
      folderOps.handleNewFolder(folderName);
    } catch (error) {
      reportClientError("Failed to load folder names:", error);
      showToast(t("errors.loadContentsFailed", { ns: "files" }), "error");
    }
  }, [folderOps, getAvailableSiblingName, nodeId, showToast, t]);

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
      void handleNewFolderClick();
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [folderOps.isCreatingFolder, handleNewFolderClick, loading, nodeId]);

  const handleRestoreLightboxFile = useCallback(
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
        reportClientError("Failed to undo media delete:", error);
        toast.error(t("preview.deleteUndoFailed", { ns: "files" }));
      }
    },
    [nodeId, reloadCurrentNode, t],
  );

  const handleLightboxDelete = useCallback(
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

  const handleCreateMarkdownFile = useCallback(async () => {
    if (!nodeId || isCreatingMarkdownFile || !ensureCurrentFolderUnlocked()) {
      return;
    }

    setIsCreatingMarkdownFile(true);

    try {
      const fileName = await getAvailableSiblingName(
        t("actions.defaultMarkdownFileName", { ns: "files" }),
      );
      const settings = await fetchServerSettings(queryClient);
      const createdFile = await uploadFileToNode({
        file: new File([""], fileName, { type: MARKDOWN_FILE_CONTENT_TYPE }),
        nodeId,
        server: {
          maxChunkSizeBytes: settings.maxChunkSizeBytes,
          supportedHashAlgorithm: settings.supportedHashAlgorithm,
        },
        encrypt: currentFolderEncryptionEnabled,
      });
      const displayFile = await applyDisplayMetaToFile(createdFile);

      useNodesStore.getState().moveFileInCache(displayFile, nodeId, nodeId);
      fileOps.handleRenameFile(displayFile.id, displayFile.name);
      void refreshNodeContent(nodeId);
    } catch (error) {
      reportClientError("Failed to create markdown file:", error);
      showToast(
        t("uploadDrop.errors.createMarkdownFileFailed", { ns: "files" }),
        "error",
      );
    } finally {
      setIsCreatingMarkdownFile(false);
    }
  }, [
    currentFolderEncryptionEnabled,
    ensureCurrentFolderUnlocked,
    fileOps,
    getAvailableSiblingName,
    isCreatingMarkdownFile,
    nodeId,
    queryClient,
    showToast,
    t,
  ]);

  return {
    fileOps,
    fileUpload,
    folderOps,
    handleCreateMarkdownFile,
    handleLightboxDelete,
    handleNewFolderClick,
    isCreatingMarkdownFile,
  };
};
