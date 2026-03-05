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
import { InterfaceLayoutType, layoutsApi } from "../../shared/api/layoutsApi";
import { shareFile } from "../../shared/utils/shareFile";
import {
  selectGallerySmoothTransitions,
  useLocalPreferencesStore,
} from "../../shared/store/localPreferencesStore";
import { getFileTypeInfo } from "../files/utils/fileTypes";
import { getFileIcon } from "../files/utils/icons";
import { useAudioPlayerStore } from "../../shared/store/audioPlayerStore";

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

  const smoothGalleryTransitions = useLocalPreferencesStore(
    selectGallerySmoothTransitions,
  );

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
      await shareFile(fileId, fileName, t, setShareToast);
    },
    [t],
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

  const audioPlaylist = useMemo(
    () =>
      sortedFiles
        .filter(
          (file) =>
            getFileTypeInfo(file.name, file.contentType ?? null).type ===
            "audio",
        )
        .map((file) => {
          const previewToken =
            file.largeFilePreviewPresignedToken ??
            file.previewHashEncryptedHex ??
            null;
          const icon = getFileIcon(previewToken, file.name, file.contentType ?? null);
          const previewUrl = typeof icon === "string" ? icon : undefined;
          return {
            id: file.id,
            name: file.name,
            nodeId: file.nodeId ?? undefined,
            previewUrl,
          };
        }),
    [sortedFiles],
  );

  const openAudio = useAudioPlayerStore((s) => s.openFromSelection);

  const handleFileClick = useCallback(
    (fileId: string, fileName: string, fileSizeBytes?: number) => {
      if (getFileTypeInfo(fileName, null).type === "audio") {
        openAudio({ fileId, fileName, playlist: audioPlaylist });
        return;
      }
      const opened = openPreview(fileId, fileName, fileSizeBytes);
      if (!opened) {
        void handleDownloadFile(fileId, fileName);
      }
    },
    [audioPlaylist, handleDownloadFile, openAudio, openPreview],
  );

  const fileListSource = useSearchFileList({
    results,
    loading,
    error: error ?? null,
    totalCount,
    hasQuery: !!query.trim(),
    rootNodeName: rootNode?.name,
  });

  const {
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    getDownloadUrl,
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
            onGoToFileLocation={({ nodeId, containerPath }) => {
              if (nodeId) {
                navigate(`/files/${nodeId}`);
                return;
              }

              const normalized = (containerPath ?? "/").trim();
              const resolvePath = normalized === "/" ? null : normalized;
              void layoutsApi.resolve({ path: resolvePath }).then((node) => {
                navigate(`/files/${node.id}`);
              });
            }}
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
          getDownloadUrl={getDownloadUrl}
          smoothTransitions={smoothGalleryTransitions}
          onClose={() => setLightboxOpen(false)}
        />
      )}
    </Box>
  );
};
