import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import {
  Alert,
  Box,
  CircularProgress,
  Dialog,
  DialogContent,
  IconButton,
  InputAdornment,
  TextField,
  Typography,
} from "@mui/material";
import { useTheme } from "@mui/material/styles";
import useMediaQuery from "@mui/material/useMediaQuery";
import { Close, Search } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { Virtuoso } from "react-virtuoso";
import { useNavigate } from "react-router-dom";
import { useRootNodeQuery } from "../../shared/api/queries/layouts";
import { useSearchFileList } from "../../shared/hooks/useFileListSource";
import type { NodeFileManifestDto } from "../../shared/api/nodesApi";
import { useFileInteractionHandlers } from "@shared/hooks/useFileInteractionHandlers";
import { FilePreviewModal, MediaLightbox } from "@shared/ui/preview";
import { getFileTypeInfo } from "@shared/utils/fileTypes";
import {
  selectGallerySmoothTransitions,
  useUserPreferencesStore,
} from "../../shared/store/userPreferencesStore";
import { useDictionaryMatch } from "./hooks/useDictionaryMatch";
import { useSearchPagination } from "./hooks/useSearchPagination";
import { useSearchHistory } from "./hooks/useSearchHistory";
import { SearchHistoryPanel } from "./components/SearchHistoryPanel";
import { SearchResultRow } from "./components/SearchResultRow";
import { SearchResultsScroller } from "./components/SearchResultsScroller";
import type { SearchDictionaryEntry, SearchRow } from "./types";

interface SearchModalProps {
  open: boolean;
  onClose: () => void;
}

interface SearchEndAdornmentProps {
  isMobile: boolean;
  onClose: () => void;
  resultCount: number;
  resultsCountText: string;
  waitingForResults: boolean;
  closeText: string;
}

const SearchEndAdornment = ({
  isMobile,
  onClose,
  resultCount,
  resultsCountText,
  waitingForResults,
  closeText,
}: SearchEndAdornmentProps) => {
  if (isMobile) {
    return (
      <IconButton
        edge="end"
        aria-label={closeText}
        title={closeText}
        onClick={onClose}
      >
        <Close />
      </IconButton>
    );
  }

  if (waitingForResults) {
    return <CircularProgress size={24} />;
  }

  if (resultCount === 0) {
    return null;
  }

  return (
    <Typography
      variant="caption"
      color="text.secondary"
      noWrap
      sx={{ px: 0.5 }}
    >
      {resultsCountText}
    </Typography>
  );
};

interface SearchResultsContentProps {
  emptyText: string;
  loadNextPage: () => void;
  loadingMore: boolean;
  renderSearchRow: (index: number, row: SearchRow) => ReactNode;
  rows: SearchRow[];
  waitingForResults: boolean;
}

const SearchResultsContent = ({
  emptyText,
  loadNextPage,
  loadingMore,
  renderSearchRow,
  rows,
  waitingForResults,
}: SearchResultsContentProps) => {
  if (waitingForResults) {
    return (
      <Box
        sx={{
          height: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        <CircularProgress size={24} />
      </Box>
    );
  }

  if (rows.length === 0) {
    return (
      <Box
        sx={{
          height: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          px: 3,
          textAlign: "center",
        }}
      >
        <Typography color="text.secondary">{emptyText}</Typography>
      </Box>
    );
  }

  return (
    <Virtuoso
      style={{ height: "100%" }}
      data={rows}
      overscan={600}
      defaultItemHeight={68}
      components={{
        Scroller: SearchResultsScroller,
        Footer: () =>
          loadingMore ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 1.5 }}>
              <CircularProgress size={18} />
            </Box>
          ) : null,
      }}
      computeItemKey={(_, row) => row.id}
      endReached={loadNextPage}
      itemContent={renderSearchRow}
    />
  );
};

export const SearchModal = ({ open, onClose }: SearchModalProps) => {
  const { t } = useTranslation("search");
  const { t: tCommon } = useTranslation("common");
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement | null>(null);
  const rootNode = useRootNodeQuery().data ?? null;
  const [failedPreviews, setFailedPreviews] = useState<Set<string>>(new Set());
  const [query, setQuery] = useState("");

  const layoutId = rootNode?.layoutId;
  const trimmedQuery = query.trim();
  const hasQuery = trimmedQuery.length > 0;

  const {
    debouncedQuery,
    results,
    totalCount,
    loadingInitial,
    loadingMore,
    error,
    loadNextPage,
  } = useSearchPagination({ trimmedQuery, layoutId });

  const hasSearchQuery = debouncedQuery.length > 0;
  const matchedDictionaryRows = useDictionaryMatch(debouncedQuery);
  const {
    entries: searchHistoryEntries,
    addQuery: addSearchHistoryQuery,
    removeQuery: removeSearchHistoryQuery,
    clear: clearSearchHistory,
  } = useSearchHistory();
  const hasSearchHistoryEntries = searchHistoryEntries.length > 0;
  const showSearchHistory = open && !hasQuery && hasSearchHistoryEntries;
  const hasExpandedSearchSurface = hasQuery || hasSearchHistoryEntries;

  useEffect(() => {
    if (!open) return;

    const rafId = window.requestAnimationFrame(() => {
      inputRef.current?.focus();
      inputRef.current?.select();
    });

    return () => window.cancelAnimationFrame(rafId);
  }, [open]);

  const fileListSource = useSearchFileList({
    results,
    loading: loadingInitial,
    error,
    totalCount,
    hasQuery: hasSearchQuery,
    rootNodeName: rootNode?.name,
  });

  const contentRows = useMemo<SearchRow[]>(() => {
    const searchRows: SearchRow[] = [];
    for (const tile of fileListSource.tiles) {
      if (tile.kind === "folder") {
        searchRows.push({
          id: `folder-${tile.node.id}`,
          kind: "folder",
          node: tile.node,
          path: tile.path,
        });
        continue;
      }

      if (!("ownerId" in tile.file) || !("metadata" in tile.file)) {
        continue;
      }

      searchRows.push({
        id: `file-${tile.file.id}`,
        kind: "file",
        file: tile.file,
        path: tile.path,
      });
    }
    return searchRows;
  }, [fileListSource.tiles]);

  const rows = useMemo(
    () => (hasSearchQuery ? [...matchedDictionaryRows, ...contentRows] : []),
    [contentRows, hasSearchQuery, matchedDictionaryRows],
  );
  const resultCount = hasSearchQuery
    ? matchedDictionaryRows.length + totalCount
    : 0;
  const waitingForResults =
    hasQuery &&
    (trimmedQuery !== debouncedQuery || (loadingInitial && rows.length === 0));

  const sortedFiles = useMemo(() => {
    if (!results) return [];
    const files = results.files.slice();
    files.sort((a, b) =>
      a.name.localeCompare(b.name, undefined, { numeric: true }),
    );
    return files;
  }, [results]);

  const smoothGalleryTransitions = useUserPreferencesStore(
    selectGallerySmoothTransitions,
  );

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
  } = useFileInteractionHandlers({ sortedFiles });

  const openFile = useCallback(
    (file: NodeFileManifestDto) => {
      const typeInfo = getFileTypeInfo(file.name, file.contentType, {
        requiresVideoTranscoding: file.requiresVideoTranscoding ?? false,
      });
      if (typeInfo.type === "image" || typeInfo.type === "video") {
        handleMediaClick(file.id);
      } else {
        void handleFileClick(file.id, file.name, file.sizeBytes);
      }

      onClose();
    },
    [handleFileClick, handleMediaClick, onClose],
  );

  const openSetting = useCallback(
    (entry: SearchDictionaryEntry) => {
      navigate(entry.path, {
        state: entry.highlightSettingId
          ? { highlightSettingId: entry.highlightSettingId }
          : undefined,
      });
      onClose();
    },
    [navigate, onClose],
  );

  const recordActiveSearchHistory = useCallback(() => {
    if (!debouncedQuery) {
      return;
    }

    addSearchHistoryQuery(debouncedQuery);
  }, [addSearchHistoryQuery, debouncedQuery]);

  const activateRow = useCallback(
    (row: SearchRow) => {
      recordActiveSearchHistory();

      if (row.kind === "setting") {
        openSetting(row.entry);
        return;
      }

      if (row.kind === "folder") {
        navigate(`/files/${row.node.id}`);
        onClose();
        return;
      }

      openFile(row.file);
    },
    [navigate, onClose, openFile, openSetting, recordActiveSearchHistory],
  );

  const handlePreviewError = useCallback((fileId: string) => {
    setFailedPreviews((prev) => new Set(prev).add(fileId));
  }, []);

  const handleShareRow = useCallback(
    (row: Extract<SearchRow, { kind: "file" }>) => {
      recordActiveSearchHistory();
      void handleShareFile(row.file.id, row.file.name);
    },
    [handleShareFile, recordActiveSearchHistory],
  );

  const handleOpenFileFolder = useCallback(
    (row: Extract<SearchRow, { kind: "file" }>) => {
      recordActiveSearchHistory();
      navigate(`/files/${row.file.nodeId}`);
      onClose();
    },
    [navigate, onClose, recordActiveSearchHistory],
  );

  const handleDownloadRow = useCallback(
    (row: Extract<SearchRow, { kind: "file" }>) => {
      recordActiveSearchHistory();
      void handleDownloadFile(row.file.id, row.file.name);
      onClose();
    },
    [handleDownloadFile, onClose, recordActiveSearchHistory],
  );

  const handleSelectSearchHistory = useCallback(
    (historyQuery: string) => {
      setQuery(historyQuery);
      addSearchHistoryQuery(historyQuery);
    },
    [addSearchHistoryQuery],
  );

  const renderSearchRow = useCallback(
    (index: number, row: SearchRow) => (
      <SearchResultRow
        row={row}
        isLast={index >= rows.length - 1}
        previewFailed={
          row.kind === "file" ? failedPreviews.has(row.file.id) : false
        }
        onPreviewError={handlePreviewError}
        onActivate={activateRow}
        onShareFile={handleShareRow}
        onOpenFileFolder={handleOpenFileFolder}
        onDownloadFile={handleDownloadRow}
      />
    ),
    [
      activateRow,
      failedPreviews,
      handleDownloadRow,
      handleOpenFileFolder,
      handlePreviewError,
      handleShareRow,
      rows.length,
    ],
  );

  return (
    <>
      <Dialog
        open={open}
        onClose={onClose}
        fullScreen={isMobile}
        maxWidth={false}
        slotProps={{
          paper: {
            sx: (theme) => ({
              width: {
                xs: "100%",
                sm: hasExpandedSearchSurface ? 880 : 720,
                lg: hasExpandedSearchSurface ? 1040 : 760,
              },
              height: {
                xs: "100%",
                sm: hasExpandedSearchSurface ? 680 : 86,
              },
              maxHeight: { xs: "100%", sm: "calc(100vh - 32px)" },
              borderRadius: { xs: 0, sm: 1.5 },
              bgcolor: "background.default",
              transition: hasSearchHistoryEntries
                ? "none"
                : theme.transitions.create(["height", "width"], {
                    duration: theme.transitions.duration.shorter,
                    easing: theme.transitions.easing.easeInOut,
                  }),
            }),
          },
        }}
      >
        <DialogContent
          sx={{
            display: "flex",
            flexDirection: "column",
            gap: 1,
            justifyContent: "flex-start",
            overflow: "hidden",
            bgcolor: "background.default",
            p: { xs: 1.5, sm: 2 },
          }}
        >
          <TextField
            fullWidth
            autoFocus
            inputRef={inputRef}
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            disabled={!layoutId}
            placeholder={t("modal.placeholder")}
            autoComplete="off"
            slotProps={{
              htmlInput: {
                autoComplete: "off",
                autoCorrect: "off",
                spellCheck: false,
              },
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <Search color="action" />
                  </InputAdornment>
                ),
                endAdornment: (
                  <InputAdornment position="end">
                    <SearchEndAdornment
                      isMobile={isMobile}
                      onClose={onClose}
                      resultCount={hasSearchQuery ? resultCount : 0}
                      resultsCountText={t("modal.resultsCount", {
                        count: resultCount,
                      })}
                      waitingForResults={waitingForResults}
                      closeText={tCommon("actions.close")}
                    />
                  </InputAdornment>
                ),
              },
            }}
          />

          {error && <Alert severity="error">{t("error")}</Alert>}

          {showSearchHistory && (
            <Box
              sx={{
                flex: 1,
                minHeight: 0,
                overflow: "auto",
                borderRadius: 1,
                bgcolor: "background.default",
              }}
            >
              <SearchHistoryPanel
                entries={searchHistoryEntries}
                onSelect={handleSelectSearchHistory}
                onRemove={removeSearchHistoryQuery}
                onClear={clearSearchHistory}
                compact={!isMobile}
              />
            </Box>
          )}

          {hasQuery && (
            <Box
              sx={{
                flex: 1,
                minHeight: 0,
                overflow: "hidden",
                borderRadius: 1,
                bgcolor: "background.default",
              }}
            >
              <SearchResultsContent
                emptyText={t("noResults")}
                loadNextPage={loadNextPage}
                loadingMore={loadingMore}
                renderSearchRow={renderSearchRow}
                rows={rows}
                waitingForResults={waitingForResults}
              />
            </Box>
          )}
        </DialogContent>
      </Dialog>

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
    </>
  );
};
