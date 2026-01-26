import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Box, Alert, Typography } from "@mui/material";
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
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import type { FileSystemTile } from "../files/types/FileListViewTypes";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";

export const SearchPage: React.FC = () => {
  const { t } = useTranslation(["search", "files"]);
  const navigate = useNavigate();
  const { rootNode, ensureHomeData } = useLayoutsStore();

  const gridHostRef = useRef<HTMLDivElement | null>(null);
  const [isAutoPageSize, setIsAutoPageSize] = useState(true);

  useEffect(() => {
    void ensureHomeData();
  }, [ensureHomeData]);

  const layoutId = rootNode?.layoutId;

  const searchState = useLayoutSearch({
    layoutId,
    pageSize: 25,
    debounceMs: 200,
  });

  const {
    query,
    page,
    pageSize: currentPageSize,
    totalCount,
    loading,
    error,
    results,
    setQuery,
    setPage,
    setPageSize,
  } = searchState;

  // Reset to the first page when query changes.
  useEffect(() => {
    setPage(1);
  }, [query, setPage]);

  // Auto-fit page size to available height (until user manually changes it).
  useEffect(() => {
    if (!isAutoPageSize) return;
    if (!gridHostRef.current) return;

    const target = gridHostRef.current;
    const rowHeight = 36; // DataGrid compact density
    const headerHeight = 56;
    const footerHeight = 56;
    const padding = 8;

    const update = () => {
      const height = target.clientHeight;
      const available = Math.max(0, height - headerHeight - footerHeight - padding);
      const fit = Math.floor(available / rowHeight);
      const fitPageSize = Math.max(10, Math.min(100, fit));

      if (fitPageSize > 0 && fitPageSize !== currentPageSize) {
        setPageSize(fitPageSize);
        setPage(1);
      }
    };

    update();

    if (typeof ResizeObserver === "undefined") {
      const onResize = () => update();
      window.addEventListener("resize", onResize);
      return () => window.removeEventListener("resize", onResize);
    }

    const observer = new ResizeObserver(() => update());
    observer.observe(target);
    return () => observer.disconnect();
  }, [
    isAutoPageSize,
    currentPageSize,
    setPage,
    setPageSize,
  ]);

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
    (fileId: string, fileName: string, fileSizeBytes?: number) => {
      const opened = openPreview(fileId, fileName, fileSizeBytes);
      if (!opened) {
        void handleDownloadFile(fileId, fileName);
      }
    },
    [handleDownloadFile, openPreview],
  );

  const sortedFiles = useMemo(() => {
    if (!results) return [];
    const files = results.files ?? [];
    const sorted = files.slice();
    sorted.sort((a, b) =>
      a.name.localeCompare(b.name, undefined, { numeric: true }),
    );
    return sorted;
  }, [results]);

  const {
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    handleMediaClick,
    setLightboxOpen,
  } = useMediaLightbox(sortedFiles);

  const tiles: FileSystemTile[] = useMemo(() => {
    if (!results) return [];

    const sortByName = <T extends { name: string }>(items: T[]): T[] => {
      const sorted = items.slice();
      sorted.sort((a, b) =>
        a.name.localeCompare(b.name, undefined, { numeric: true }),
      );
      return sorted;
    };

    const sortedFolders = sortByName(results.nodes ?? []);
    const sortedFiles = sortByName(results.files ?? []);

    return [
      ...sortedFolders.map((node) => ({ kind: "folder", node }) as const),
      ...sortedFiles.map((file) => ({ kind: "file", file }) as const),
    ];
  }, [results]);

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
    <Box
      pb={1}
      width="100%"
      sx={{ display: "flex", flexDirection: "column", flex: 1, minHeight: 0 }}
    >
      <SearchBar
        value={query}
        onChange={(value) => {
          setIsAutoPageSize(true);
          setQuery(value);
        }}
        disabled={!layoutId}
        placeholder={t("searchPlaceholder", { ns: "search" })}
      />

      {error && (
        <Box mb={2}>
          <Alert severity="error">{t("error", { ns: "search" })}</Alert>
        </Box>
      )}

      {!loading &&
        layoutId &&
        !query.trim() &&
        !results && (
          <Typography color="text.secondary">
            {t("enterQueryHint", { ns: "search" })}
          </Typography>
        )}

      {query.trim() && (loading || results) && (
        <Box
          ref={gridHostRef}
          sx={{ width: "100%", flex: 1, minHeight: 0 }}
        >
          <FileListViewFactory
            layoutType={InterfaceLayoutType.List}
            tiles={results ? tiles : []}
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
            pagination={{
              page: Math.max(0, page - 1),
              pageSize: currentPageSize,
              totalCount,
              loading,
              onPageChange: (newPage) => {
                setPage(newPage + 1);
              },
              onPageSizeChange: (newPageSize) => {
                setIsAutoPageSize(false);
                setPageSize(newPageSize);
                setPage(1);
              },
            }}
          />
        </Box>
      )}

      <FilePreviewModal
        isOpen={previewState.isOpen}
        fileId={previewState.fileId}
        fileName={previewState.fileName}
        fileType={previewState.fileType}
        fileSizeBytes={previewState.fileSizeBytes}
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
