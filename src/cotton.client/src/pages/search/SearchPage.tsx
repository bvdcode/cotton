import React, { useCallback, useEffect, useMemo } from "react";
import { Box, Alert, CircularProgress, Typography } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useLayoutsStore } from "../../shared/store/layoutsStore";
import { SearchBar } from "./components/SearchBar";
import { useLayoutSearch } from "./hooks/useLayoutSearch";
import { downloadFile } from "../files/utils/fileHandlers";
import { useFilePreview } from "../files/hooks/useFilePreview";
import { FilePreviewModal } from "../files/components";
import { FileListViewFactory } from "../files/components";
import { MediaLightbox } from "../files/components";
import { useFolderOperations } from "../files/hooks/useFolderOperations";
import { useFileOperations } from "../files/hooks/useFileOperations";
import { useMediaLightbox } from "../files/hooks/useMediaLightbox";
import { buildFolderOperations, buildFileOperations } from "../../shared/utils/operationsAdapters";
import type { FileSystemTile } from "../files/types/FileListViewTypes";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";

export const SearchPage: React.FC = () => {
  const { t } = useTranslation(["search", "files"]);
  const navigate = useNavigate();
  const { rootNode, ensureHomeData } = useLayoutsStore();

  useEffect(() => {
    void ensureHomeData();
  }, [ensureHomeData]);

  const layoutId = rootNode?.layoutId;

  const searchState = useLayoutSearch({
    layoutId,
    pageSize: 100,
    debounceMs: 1000,
  });

  const { previewState, openPreview, closePreview } = useFilePreview();

  const handleFolderClick = useCallback(
    (nodeId: string) => {
      navigate(`/files/${nodeId}`);
    },
    [navigate],
  );

  const handleDownloadFile = useCallback(
    async (fileId: string, fileName: string) => {
      await downloadFile(fileId, fileName);
    },
    [],
  );

  const handleFileClick = useCallback(
    async (fileId: string, fileName: string) => {
      const opened = openPreview(fileId, fileName);
      if (!opened) {
        await handleDownloadFile(fileId, fileName);
      }
    },
    [handleDownloadFile, openPreview],
  );

  const sortedFiles = useMemo(() => {
    if (!searchState.results) return [];
    const files = searchState.results.files ?? [];
    const sorted = files.slice();
    sorted.sort((a, b) =>
      a.name.localeCompare(b.name, undefined, { numeric: true }),
    );
    return sorted;
  }, [searchState.results]);

  const {
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    handleMediaClick,
    setLightboxOpen,
  } = useMediaLightbox(sortedFiles);

  const tiles: FileSystemTile[] = useMemo(() => {
    if (!searchState.results) return [];

    const sortByName = <T extends { name: string }>(items: T[]): T[] => {
      const sorted = items.slice();
      sorted.sort((a, b) =>
        a.name.localeCompare(b.name, undefined, { numeric: true }),
      );
      return sorted;
    };

    const sortedFolders = sortByName(searchState.results.nodes ?? []);
    const sortedFiles = sortByName(searchState.results.files ?? []);

    return [
      ...sortedFolders.map((node) => ({ kind: "folder", node }) as const),
      ...sortedFiles.map((file) => ({ kind: "file", file }) as const),
    ];
  }, [searchState.results]);

  const rawFolderOps = useFolderOperations(null);
  const rawFileOps = useFileOperations();

  const folderOperations = useMemo(
    () =>
      buildFolderOperations(rawFolderOps, (folderId) => {
        handleFolderClick(folderId);
      }),
    [rawFolderOps, handleFolderClick],
  );

  const fileOperations = useMemo(
    () =>
      buildFileOperations(rawFileOps, {
        onDownload: handleDownloadFile,
        onClick: (fileId, fileName) => {
          void handleFileClick(fileId, fileName);
        },
        onMediaClick: handleMediaClick,
      }),
    [rawFileOps, handleDownloadFile, handleFileClick, handleMediaClick],
  );

  return (
    <Box p={3} width="100%">
      <SearchBar
        value={searchState.query}
        onChange={searchState.setQuery}
        disabled={!layoutId}
        placeholder={t("searchPlaceholder", { ns: "search", defaultValue: "Search files and folders..." })}
      />

      {searchState.error && (
        <Box mb={2}>
          <Alert severity="error">
            {t(searchState.error, { ns: "search", defaultValue: "Search failed. Please try again." })}
          </Alert>
        </Box>
      )}

      {searchState.loading && (
        <Box display="flex" justifyContent="center" mt={2}>
          <CircularProgress size={24} />
        </Box>
      )}

      {!searchState.loading && layoutId && !searchState.query.trim() && !searchState.results && (
        <Typography color="text.secondary">
          {t("enterQueryHint", { ns: "search", defaultValue: "Start typing to search..." })}
        </Typography>
      )}

      {!searchState.loading && !searchState.query.trim() && searchState.results && tiles.length === 0 && (
        <Typography color="text.secondary">
          {t("noResults", { ns: "search", defaultValue: "No files or folders found" })}
        </Typography>
      )}

      {tiles.length > 0 && (
        <FileListViewFactory
          layoutType={InterfaceLayoutType.List}
          tiles={tiles}
          folderOperations={folderOperations}
          fileOperations={fileOperations}
          isCreatingFolder={false}
          newFolderName=""
          onNewFolderNameChange={() => {}}
          onConfirmNewFolder={async () => {}}
          onCancelNewFolder={() => {}}
          folderNamePlaceholder={t("actions.folderNamePlaceholder", {
            ns: "files",
          })}
          fileNamePlaceholder={t("rename.fileNamePlaceholder", {
            ns: "files",
          })}
        />
      )}

      <FilePreviewModal
        isOpen={previewState.isOpen}
        fileId={previewState.fileId}
        fileName={previewState.fileName}
        fileType={previewState.fileType}
        onClose={closePreview}
      />

      {lightboxOpen && mediaItems.length > 0 && (
        <MediaLightbox
          open={lightboxOpen}
          initialIndex={lightboxIndex}
          items={mediaItems}
          getSignedMediaUrl={getSignedMediaUrl}
          onClose={() => setLightboxOpen(false)}
        />
      )}
    </Box>
  );
};
