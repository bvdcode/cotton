import React, { useCallback, useEffect, useMemo, useRef } from "react";
import { Box, Alert, Typography } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useRootNodeQuery } from "../../shared/api/queries/layouts";
import { SearchBar } from "./components/SearchBar";
import { useLayoutSearch } from "./hooks/useLayoutSearch";
import { SearchHistoryPanel } from "../../features/search/components/SearchHistoryPanel";
import { useSearchHistory } from "../../features/search/hooks/useSearchHistory";
import { FileListViewFactory } from "../files/components";
import { FilePreviewModal, MediaLightbox } from "@shared/ui/preview";
import { useFolderOperations } from "../files/hooks/useFolderOperations";
import { useFileOperations } from "../files/hooks/useFileOperations";
import { useSearchFileList } from "../../shared/hooks/useFileListSource";
import { useFileListPageLogic } from "../files/hooks/useFileListPageLogic";
import {
  buildFolderOperations,
  buildFileOperations,
} from "../../shared/utils/operationsAdapters";
import { InterfaceLayoutType, layoutsApi } from "../../shared/api/layoutsApi";
import {
  selectGallerySmoothTransitions,
  useUserPreferencesStore,
} from "../../shared/store/userPreferencesStore";
import { usePageTitle } from "../../shared/hooks/usePageTitle";

export const SearchPage: React.FC = () => {
  const { t } = useTranslation(["search", "files"]);
  const navigate = useNavigate();
  const rootNode = useRootNodeQuery().data ?? null;

  const gridHostRef = useRef<HTMLDivElement | null>(null);
  usePageTitle(t("title", { ns: "search" }));

  const layoutId = rootNode?.layoutId;

  const smoothGalleryTransitions = useUserPreferencesStore(
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
    completedQuery,
    setQuery,
    setPage,
    setPageSize,
  } = searchState;
  const trimmedSearchQuery = query.trim();
  const {
    entries: searchHistoryEntries,
    addQuery: addSearchHistoryQuery,
    removeQuery: removeSearchHistoryQuery,
    clear: clearSearchHistory,
  } = useSearchHistory();
  const showSearchHistory = Boolean(
    layoutId &&
    !trimmedSearchQuery &&
    !results &&
    searchHistoryEntries.length > 0,
  );

  // Reset to the first page when query changes.
  useEffect(() => {
    setPage(1);
  }, [query, setPage]);

  const recordActiveSearchHistory = useCallback(() => {
    if (!completedQuery) {
      return;
    }

    addSearchHistoryQuery(completedQuery);
  }, [addSearchHistoryQuery, completedQuery]);

  const handleFolderClick = useCallback(
    (nodeId: string) => {
      recordActiveSearchHistory();
      navigate(`/files/${nodeId}`);
    },
    [navigate, recordActiveSearchHistory],
  );

  const handleSelectSearchHistory = useCallback(
    (historyQuery: string) => {
      setQuery(historyQuery);
      addSearchHistoryQuery(historyQuery);
    },
    [addSearchHistoryQuery, setQuery],
  );

  const fileListSource = useSearchFileList({
    results,
    loading,
    error: error ?? null,
    totalCount,
    hasQuery: !!trimmedSearchQuery,
    rootNodeName: rootNode?.name,
  });

  const fileListLogic = useFileListPageLogic({
    source: fileListSource,
    sourceKind: "search",
  });

  const {
    previewState,
    closePreview,
    handleFileClick,
    handleDownloadFile,
    handleShareFile,
    lightboxOpen,
    lightboxIndex,
    mediaItems,
    getSignedMediaUrl,
    getDownloadUrl,
    handleMediaClick,
    setLightboxOpen,
  } = fileListLogic.interaction;

  const { tiles } = fileListLogic;

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
        onDownload: (fileId, fileName) => {
          recordActiveSearchHistory();
          return handleDownloadFile(fileId, fileName);
        },
        onShare: (fileId, fileName) => {
          recordActiveSearchHistory();
          return handleShareFile(fileId, fileName);
        },
        onClick: (fileId, fileName, fileSizeBytes) => {
          recordActiveSearchHistory();
          void handleFileClick(fileId, fileName, fileSizeBytes);
        },
        onMediaClick: (fileId) => {
          recordActiveSearchHistory();
          return handleMediaClick(fileId);
        },
      }),
    [
      rawFileOps,
      handleDownloadFile,
      handleShareFile,
      handleFileClick,
      handleMediaClick,
      recordActiveSearchHistory,
    ],
  );

  return (
    <Box
      pb={1}
      width="100%"
      sx={{ display: "flex", flexDirection: "column", flex: 1, minHeight: 0 }}
    >
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

      {!loading && showSearchHistory && (
        <Box
          width="100%"
          maxWidth={760}
          mx="auto"
          sx={{
            border: 1,
            borderColor: "divider",
            borderRadius: 1,
            overflow: "hidden",
          }}
        >
          <SearchHistoryPanel
            entries={searchHistoryEntries}
            onSelect={handleSelectSearchHistory}
            onRemove={removeSearchHistoryQuery}
            onClear={clearSearchHistory}
          />
        </Box>
      )}

      {!loading &&
        layoutId &&
        !trimmedSearchQuery &&
        !results &&
        !showSearchHistory && (
          <Box
            flex={1}
            minHeight={0}
            display="flex"
            flexDirection="column"
            alignItems="center"
            justifyContent="center"
            textAlign="center"
            gap={1.5}
            sx={{ color: "text.secondary" }}
          >
            <SearchIcon sx={{ fontSize: 48, opacity: 0.5 }} />
            <Typography color="text.secondary">
              {t("enterQueryHint", { ns: "search" })}
            </Typography>
          </Box>
        )}

      {(loading || results) && (
        <Box ref={gridHostRef} sx={{ width: "100%", flex: 1, minHeight: 0 }}>
          <FileListViewFactory
            layoutType={InterfaceLayoutType.List}
            tiles={results ? tiles : []}
            folderOperations={folderOperations}
            fileOperations={fileOperations}
            onGoToFileLocation={({ nodeId, containerPath }) => {
              recordActiveSearchHistory();

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
        file={previewState.file}
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
