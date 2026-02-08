import React, { useCallback, useEffect, useMemo, useRef } from "react";
import { Box, Alert, Snackbar, Typography } from "@mui/material";
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
import { useSearchFileList } from "../../shared/hooks/useFileListSource";
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import { InterfaceLayoutType } from "../../shared/api/layoutsApi";
import { filesApi } from "../../shared/api/filesApi";
import { shareLinks } from "../../shared/utils/shareLinks";

export const SearchPage: React.FC = () => {
  const { t } = useTranslation(["search", "files"]);
  const navigate = useNavigate();
  const { rootNode, ensureHomeData } = useLayoutsStore();

  const gridHostRef = useRef<HTMLDivElement | null>(null);
  useEffect(() => {
    document.title = "Cotton - Search";

    return () => {
      document.title = "Cotton";
    };
  }, []);

  useEffect(() => {
    void ensureHomeData();
  }, [ensureHomeData]);

  const layoutId = rootNode?.layoutId;

  const searchState = useLayoutSearch({
    layoutId,
    debounceMs: 200,
  });

  const {
    query,
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

  const [shareToast, setShareToast] = React.useState<{
    open: boolean;
    message: string;
  }>({ open: false, message: "" });

  const handleShareFile = useCallback(
    async (fileId: string, fileName: string) => {
      try {
        const downloadLink = await filesApi.getDownloadLink(
          fileId,
          60 * 24 * 365,
        );

        const token = shareLinks.tryExtractTokenFromDownloadUrl(downloadLink);
        if (!token) {
          setShareToast({
            open: true,
            message: t("share.errors.token", { ns: "files" }),
          });
          return;
        }

        const url = shareLinks.buildShareUrl(token);

        if (typeof navigator !== "undefined" && typeof navigator.share === "function") {
          try {
            await navigator.share({ title: fileName, url });
            setShareToast({
              open: true,
              message: t("share.shared", { ns: "files", name: fileName }),
            });
            return;
          } catch (e) {
            if (e instanceof Error && e.name === "AbortError") {
              return;
            }
          }
        }

        try {
          await navigator.clipboard.writeText(url);
          setShareToast({
            open: true,
            message: t("share.copied", { ns: "files", name: fileName }),
          });
        } catch {
          setShareToast({
            open: true,
            message: t("share.errors.copy", { ns: "files" }),
          });
        }
      } catch {
        setShareToast({
          open: true,
          message: t("share.errors.link", { ns: "files" }),
        });
      }
    },
    [t],
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

  const fileListSource = useSearchFileList({
    results,
    loading,
    error: error ?? null,
    totalCount,
    hasQuery: !!query.trim(),
  });

  const {
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    handleMediaClick,
    setLightboxOpen,
  } = useMediaLightbox(sortedFiles);

  const tiles = fileListSource.tiles;

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
        onShare: handleShareFile,
        onClick: (fileId, fileName, fileSizeBytes) => {
          void handleFileClick(fileId, fileName, fileSizeBytes);
        },
        onMediaClick: handleMediaClick,
      }),
    [rawFileOps, handleDownloadFile, handleShareFile, handleFileClick, handleMediaClick],
  );

  return (
    <Box
      pb={1}
      width="100%"
      sx={{ display: "flex", flexDirection: "column", flex: 1, minHeight: 0 }}
    >
      <Snackbar
        open={shareToast.open}
        autoHideDuration={2500}
        onClose={() => setShareToast((prev) => ({ ...prev, open: false }))}
        message={shareToast.message}
      />
      <SearchBar
        value={query}
        onChange={setQuery}
        disabled={!layoutId}
        placeholder={t("searchPlaceholder", { ns: "search" })}
      />

      {error && (
        <Box mb={2}>
          <Alert severity="error">{t("error", { ns: "search" })}</Alert>
        </Box>
      )}

      {!loading && layoutId && !query.trim() && !results && (
        <Typography color="text.secondary">
          {t("enterQueryHint", { ns: "search" })}
        </Typography>
      )}

      {(loading || results) && (
        <Box ref={gridHostRef} sx={{ width: "100%", flex: 1, minHeight: 0 }}>
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
              totalCount,
              loading,
              onPaginationModelChange: (model) => {
                setPage(model.page + 1);
                setPageSize(model.pageSize);
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
