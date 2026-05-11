import {
  forwardRef,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ComponentPropsWithoutRef,
} from "react";
import {
  Alert,
  Box,
  ButtonBase,
  CircularProgress,
  Dialog,
  DialogContent,
  IconButton,
  InputAdornment,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import { useTheme } from "@mui/material/styles";
import useMediaQuery from "@mui/material/useMediaQuery";
import {
  Article,
  Close,
  Download,
  Folder,
  FolderOpen,
  Image as ImageIcon,
  InsertDriveFile,
  OpenInNew,
  Search,
  Settings,
  Share,
  TextSnippet,
  VideoFile,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { Virtuoso } from "react-virtuoso";
import { useNavigate } from "react-router-dom";
import { UserRole } from "../auth/types";
import { useAuthStore } from "../../shared/store/authStore";
import { useLayoutsStore } from "../../shared/store/layoutsStore";
import { useSearchFileList } from "../../shared/hooks/useFileListSource";
import { formatBytes } from "../../shared/utils/formatBytes";
import {
  layoutsApi,
  type LayoutSearchResultDto,
  type NodeDto,
} from "../../shared/api/layoutsApi";
import type { NodeFileManifestDto } from "../../shared/api/nodesApi";
import { useFileInteractionHandlers } from "../../pages/files/hooks/useFileInteractionHandlers";
import { FilePreviewModal, MediaLightbox } from "../../pages/files/components";
import {
  selectGallerySmoothTransitions,
  useLocalPreferencesStore,
} from "../../shared/store/localPreferencesStore";
import {
  getFileTypeInfo,
  isImageFile,
  isPdfFile,
  isTextFile,
  isVideoFile,
} from "../../pages/files/utils/fileTypes";

type SearchDictionaryEntry = {
  id: string;
  title: string;
  description?: string;
  path: string;
  keywords: string[];
  highlightSettingId?: string;
  adminOnly?: boolean;
};

type SearchRow =
  | {
      id: string;
      kind: "setting";
      entry: SearchDictionaryEntry;
    }
  | {
      id: string;
      kind: "folder";
      node: NodeDto;
      path?: string;
    }
  | {
      id: string;
      kind: "file";
      file: NodeFileManifestDto;
      path?: string;
    };

type SearchSettingRow = Extract<SearchRow, { kind: "setting" }>;
type DictionaryMatch = { row: SearchSettingRow; score: number };

interface SearchModalProps {
  open: boolean;
  onClose: () => void;
}

const MIN_SETTING_QUERY_LENGTH = 3;
const SEARCH_PAGE_SIZE = 80;
const SEARCH_DEBOUNCE_MS = 260;

const normalizeSearchText = (value: string): string =>
  value
    .toLocaleLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "");

const normalizeCompactSearchText = (value: string): string =>
  normalizeSearchText(value).replace(/[\s._\-/:\\]+/g, "");

const isDictionaryEntry = (value: unknown): value is SearchDictionaryEntry => {
  if (!value || typeof value !== "object") return false;

  const record = value as Record<string, unknown>;
  const keywords = record.keywords;

  return (
    typeof record.id === "string" &&
    typeof record.title === "string" &&
    typeof record.path === "string" &&
    Array.isArray(keywords) &&
    keywords.every((keyword) => typeof keyword === "string") &&
    (record.description === undefined || typeof record.description === "string") &&
    (record.highlightSettingId === undefined ||
      typeof record.highlightSettingId === "string") &&
    (record.adminOnly === undefined || typeof record.adminOnly === "boolean")
  );
};

const getSmallFileIcon = (fileName: string) => {
  const iconSx = { fontSize: 28 };
  if (isTextFile(fileName)) return <Article color="action" sx={iconSx} />;
  if (isImageFile(fileName)) return <ImageIcon color="action" sx={iconSx} />;
  if (isVideoFile(fileName)) return <VideoFile color="action" sx={iconSx} />;
  if (isPdfFile(fileName)) return <TextSnippet color="action" sx={iconSx} />;
  return <InsertDriveFile color="action" sx={iconSx} />;
};

const mergeSearchResults = (
  previous: LayoutSearchResultDto | null,
  next: LayoutSearchResultDto,
): LayoutSearchResultDto => {
  if (!previous) return next;

  return {
    nodes: [...(previous.nodes ?? []), ...(next.nodes ?? [])],
    files: [...(previous.files ?? []), ...(next.files ?? [])],
    nodePaths: {
      ...(previous.nodePaths ?? {}),
      ...(next.nodePaths ?? {}),
    },
    filePaths: {
      ...(previous.filePaths ?? {}),
      ...(next.filePaths ?? {}),
    },
  };
};

const SearchResultsScroller = forwardRef<
  HTMLDivElement,
  ComponentPropsWithoutRef<"div">
>((props, ref) => (
  <Box
    ref={ref}
    {...props}
    sx={{
      overflowX: "hidden",
      scrollbarWidth: "thin",
      "&::-webkit-scrollbar": {
        width: 8,
      },
      "&::-webkit-scrollbar-track": {
        bgcolor: "transparent",
      },
      "&::-webkit-scrollbar-thumb": {
        bgcolor: "action.disabled",
        borderRadius: 1,
      },
      "&::-webkit-scrollbar-thumb:hover": {
        bgcolor: "action.active",
      },
    }}
  />
));

SearchResultsScroller.displayName = "SearchResultsScroller";

export const SearchModal = ({ open, onClose }: SearchModalProps) => {
  const { t } = useTranslation("search");
  const { t: tCommon } = useTranslation("common");
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement | null>(null);
  const searchGenerationRef = useRef(0);
  const requestedPageRef = useRef(0);
  const userRole = useAuthStore((s) => s.user?.role ?? null);
  const { rootNode, ensureHomeData } = useLayoutsStore();
  const [failedPreviews, setFailedPreviews] = useState<Set<string>>(new Set());
  const [query, setQuery] = useState("");
  const [debouncedQuery, setDebouncedQuery] = useState("");
  const [results, setResults] = useState<LayoutSearchResultDto | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [loadedPage, setLoadedPage] = useState(0);
  const [loadingInitial, setLoadingInitial] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const layoutId = rootNode?.layoutId;
  const trimmedQuery = query.trim();
  const hasQuery = trimmedQuery.length > 0;
  const hasSearchQuery = debouncedQuery.length > 0;

  useEffect(() => {
    if (!open) return;
    void ensureHomeData();
  }, [ensureHomeData, open]);

  useEffect(() => {
    if (!open) return;

    const rafId = window.requestAnimationFrame(() => {
      inputRef.current?.focus();
      inputRef.current?.select();
    });

    return () => window.cancelAnimationFrame(rafId);
  }, [open]);

  useEffect(() => {
    if (!trimmedQuery) {
      setDebouncedQuery("");
      return;
    }

    const handle = window.setTimeout(() => {
      setDebouncedQuery(trimmedQuery);
    }, SEARCH_DEBOUNCE_MS);

    return () => window.clearTimeout(handle);
  }, [trimmedQuery]);

  const fetchSearchPage = useCallback(
    async (
      pageToLoad: number,
      mode: "replace" | "append",
      generation = searchGenerationRef.current,
    ) => {
      if (!layoutId || !debouncedQuery) return;

      setError(null);
      if (mode === "replace") {
        setLoadingInitial(true);
      } else {
        setLoadingMore(true);
      }

      try {
        const response = await layoutsApi.search({
          layoutId,
          query: debouncedQuery,
          page: pageToLoad,
          pageSize: SEARCH_PAGE_SIZE,
        });

        if (generation !== searchGenerationRef.current) return;

        setResults((previous) =>
          mode === "replace"
            ? response.data
            : mergeSearchResults(previous, response.data),
        );
        setTotalCount(response.totalCount);
        setLoadedPage(pageToLoad);
      } catch (err) {
        if (generation !== searchGenerationRef.current) return;
        requestedPageRef.current = Math.max(0, pageToLoad - 1);
        console.error("Failed to search layouts", err);
        setError("searchFailed");
      } finally {
        if (generation !== searchGenerationRef.current) return;
        if (mode === "replace") {
          setLoadingInitial(false);
        } else {
          setLoadingMore(false);
        }
      }
    },
    [debouncedQuery, layoutId],
  );

  useEffect(() => {
    const generation = searchGenerationRef.current + 1;
    searchGenerationRef.current = generation;
    setResults(null);
    setTotalCount(0);
    setLoadedPage(0);
    requestedPageRef.current = 0;
    setLoadingInitial(false);
    setLoadingMore(false);
    setError(null);

    if (!layoutId || !debouncedQuery) return;

    requestedPageRef.current = 1;
    void fetchSearchPage(1, "replace", generation);
  }, [debouncedQuery, fetchSearchPage, layoutId]);

  const rawDictionary = t("dictionary", { returnObjects: true }) as unknown;

  const dictionaryEntries = useMemo(() => {
    const entries = Array.isArray(rawDictionary)
      ? rawDictionary.filter(isDictionaryEntry)
      : [];

    return entries.filter(
      (entry) => !entry.adminOnly || userRole === UserRole.Admin,
    );
  }, [rawDictionary, userRole]);

  const matchedDictionaryRows = useMemo(() => {
    const normalizedQuery = normalizeSearchText(debouncedQuery);
    const compactQuery = normalizeCompactSearchText(debouncedQuery);
    if (normalizedQuery.length < MIN_SETTING_QUERY_LENGTH) return [];

    return dictionaryEntries
      .map<DictionaryMatch | null>((entry) => {
        const normalizedTitle = normalizeSearchText(entry.title);
        const normalizedKeywords = entry.keywords.map(normalizeSearchText);
        const normalizedDescription = normalizeSearchText(entry.description ?? "");
        const haystack = [
          normalizedTitle,
          normalizedDescription,
          normalizeSearchText(entry.path),
          ...normalizedKeywords,
        ].join(" ");
        const compactKeywords = entry.keywords.map(normalizeCompactSearchText);
        const compactHaystack = [
          normalizeCompactSearchText(entry.title),
          normalizeCompactSearchText(entry.description ?? ""),
          normalizeCompactSearchText(entry.path),
          ...compactKeywords,
        ].join(" ");

        const matchesCompact =
          compactQuery.length > 0 && compactHaystack.includes(compactQuery);

        if (!haystack.includes(normalizedQuery) && !matchesCompact) {
          return null;
        }

        const keywordStarts = normalizedKeywords.some((keyword) =>
          keyword.startsWith(normalizedQuery),
        );
        const compactKeywordStarts =
          compactQuery.length > 0 &&
          compactKeywords.some((keyword) => keyword.startsWith(compactQuery));
        const score = normalizedTitle.startsWith(normalizedQuery)
          ? 0
          : keywordStarts || compactKeywordStarts
            ? 1
            : normalizedTitle.includes(normalizedQuery) ||
                (compactQuery.length > 0 &&
                  normalizeCompactSearchText(entry.title).includes(compactQuery))
              ? 2
              : 3;

        return {
          row: {
            id: `setting-${entry.id}`,
            kind: "setting" as const,
            entry,
          },
          score,
        };
      })
      .filter((match): match is DictionaryMatch => Boolean(match))
      .sort((a, b) => a.score - b.score || a.row.id.localeCompare(b.row.id))
      .map((match) => match.row);
  }, [debouncedQuery, dictionaryEntries]);

  const fileListSource = useSearchFileList({
    results,
    loading: loadingInitial,
    error,
    totalCount,
    hasQuery: hasSearchQuery,
    rootNodeName: rootNode?.name,
  });

  const contentRows = useMemo<SearchRow[]>(
    () =>
      fileListSource.tiles.map((tile) => {
        if (tile.kind === "folder") {
          return {
            id: `folder-${tile.node.id}`,
            kind: "folder",
            node: tile.node,
            path: tile.path,
          };
        }

        return {
          id: `file-${tile.file.id}`,
          kind: "file",
          file: tile.file as NodeFileManifestDto,
          path: tile.path,
        };
      }),
    [fileListSource.tiles],
  );

  const rows = useMemo(
    () => (hasSearchQuery ? [...matchedDictionaryRows, ...contentRows] : []),
    [contentRows, hasSearchQuery, matchedDictionaryRows],
  );
  const loadedContentCount =
    (results?.nodes?.length ?? 0) + (results?.files?.length ?? 0);
  const hasMoreContent = hasSearchQuery && loadedContentCount < totalCount;
  const resultCount = hasSearchQuery ? matchedDictionaryRows.length + totalCount : 0;
  const waitingForResults =
    hasQuery &&
    (trimmedQuery !== debouncedQuery || (loadingInitial && rows.length === 0));

  const loadNextPage = useCallback(() => {
    if (!hasMoreContent || loadingInitial || loadingMore || loadedPage <= 0) {
      return;
    }

    const nextPage = loadedPage + 1;
    if (requestedPageRef.current >= nextPage) {
      return;
    }

    requestedPageRef.current = nextPage;
    void fetchSearchPage(nextPage, "append");
  }, [
    fetchSearchPage,
    hasMoreContent,
    loadedPage,
    loadingInitial,
    loadingMore,
  ]);

  const sortedFiles = useMemo(() => {
    if (!results) return [];
    const files = results.files.slice();
    files.sort((a, b) =>
      a.name.localeCompare(b.name, undefined, { numeric: true }),
    );
    return files;
  }, [results]);

  const smoothGalleryTransitions = useLocalPreferencesStore(
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
  } = useFileInteractionHandlers({
    sortedFiles,
  });

  const openFile = useCallback(
    (file: NodeFileManifestDto) => {
      const typeInfo = getFileTypeInfo(file.name, file.contentType);
      if (typeInfo.type === "image" || typeInfo.type === "video") {
        handleMediaClick(file.id);
      } else {
        void handleFileClick(file.id, file.name, file.sizeBytes);
      }

      onClose();
    },
    [handleFileClick, handleMediaClick, onClose],
  );

  const shareSearchFile = useCallback(
    (file: NodeFileManifestDto) => {
      void handleShareFile(file.id, file.name);
    },
    [handleShareFile],
  );

  const downloadSearchFile = useCallback(
    (file: NodeFileManifestDto) => {
      void handleDownloadFile(file.id, file.name);
      onClose();
    },
    [handleDownloadFile, onClose],
  );

  const openFolder = useCallback(
    (nodeId: string) => {
      navigate(`/files/${nodeId}`);
      onClose();
    },
    [navigate, onClose],
  );

  const openFileFolder = useCallback(
    (file: NodeFileManifestDto) => {
      navigate(`/files/${file.nodeId}`);
      onClose();
    },
    [navigate, onClose],
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

  const renderPreview = useCallback((row: SearchRow) => {
    if (row.kind === "setting") {
      return <Settings color="primary" sx={{ fontSize: 28 }} />;
    }

    if (row.kind === "folder") {
      return <Folder color="primary" sx={{ fontSize: 30 }} />;
    }

    const previewHash = row.file.previewHashEncryptedHex;
    const previewUrl =
      previewHash && !failedPreviews.has(row.file.id)
        ? `/api/v1/preview/${encodeURIComponent(previewHash)}.webp`
        : null;

    if (previewUrl) {
      return (
        <Box
          component="img"
          src={previewUrl}
          alt=""
          loading="lazy"
          onError={() => {
            setFailedPreviews((prev) => new Set(prev).add(row.file.id));
          }}
          sx={{
            width: "100%",
            height: "100%",
            objectFit: "cover",
            borderRadius: 1,
          }}
        />
      );
    }

    return getSmallFileIcon(row.file.name);
  }, [failedPreviews]);

  const getRowText = useCallback((row: SearchRow) => {
    if (row.kind === "setting") {
      return {
        title: row.entry.title,
        meta: row.entry.description ?? t("types.setting"),
        action: t("actions.openSetting"),
      };
    }

    if (row.kind === "folder") {
      return {
        title: row.node.name,
        meta: row.path ?? t("types.folder"),
        action: t("actions.openFolder"),
      };
    }

    const size = formatBytes(row.file.sizeBytes);
    return {
      title: row.file.name,
      meta: row.path ? `${row.path} - ${size}` : size,
      action: t("actions.openFile"),
    };
  }, [t]);

  const activateRow = useCallback((row: SearchRow) => {
    if (row.kind === "setting") {
      openSetting(row.entry);
      return;
    }

    if (row.kind === "folder") {
      openFolder(row.node.id);
      return;
    }

    openFile(row.file);
  }, [openFile, openFolder, openSetting]);

  const renderSearchRow = useCallback(
    (index: number, row: SearchRow) => {
      const text = getRowText(row);
      const fileTypeInfo =
        row.kind === "file"
          ? getFileTypeInfo(row.file.name, row.file.contentType)
          : null;
      const primaryAction =
        row.kind === "file" && fileTypeInfo?.supportsInlineView === false
          ? t("actions.downloadFile")
          : text.action;
      return (
        <ButtonBase
          onClick={() => activateRow(row)}
          sx={{
            width: "100%",
            minHeight: 68,
            justifyContent: "stretch",
            textAlign: "left",
            px: 1.25,
            py: 1,
            borderBottom: index < rows.length - 1 ? 1 : 0,
            borderColor: "divider",
            bgcolor: "background.default",
            "&:hover": {
              bgcolor: "action.hover",
            },
          }}
        >
          <Stack
            direction="row"
            spacing={1.25}
            alignItems="center"
            width="100%"
            minWidth={0}
          >
            <Box
              sx={{
                width: 44,
                height: 44,
                flexShrink: 0,
                borderRadius: 1,
                bgcolor: "action.hover",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                overflow: "hidden",
              }}
            >
              {renderPreview(row)}
            </Box>

            <Stack spacing={0.25} minWidth={0} flex={1}>
              <Typography
                variant="body2"
                fontWeight={700}
                noWrap
                title={text.title}
              >
                {text.title}
              </Typography>
              <Typography
                variant="caption"
                color="text.secondary"
                noWrap
                title={text.meta}
              >
                {text.meta}
              </Typography>
            </Stack>

            {row.kind === "file" && (
              <>
                <Tooltip title={t("actions.shareFile")}>
                  <IconButton
                    size="small"
                    aria-label={t("actions.shareFile")}
                    onClick={(event) => {
                      event.stopPropagation();
                      shareSearchFile(row.file);
                    }}
                    sx={{ flexShrink: 0 }}
                  >
                    <Share fontSize="small" />
                  </IconButton>
                </Tooltip>
                <Tooltip title={t("actions.openContainingFolder")}>
                  <IconButton
                    size="small"
                    aria-label={t("actions.openContainingFolder")}
                    onClick={(event) => {
                      event.stopPropagation();
                      openFileFolder(row.file);
                    }}
                    sx={{ flexShrink: 0 }}
                  >
                    <FolderOpen fontSize="small" />
                  </IconButton>
                </Tooltip>
              </>
            )}

            <Tooltip title={primaryAction}>
              <IconButton
                size="small"
                aria-label={primaryAction}
                onClick={(event) => {
                  event.stopPropagation();
                  if (
                    row.kind === "file" &&
                    fileTypeInfo?.supportsInlineView === false
                  ) {
                    downloadSearchFile(row.file);
                    return;
                  }
                  activateRow(row);
                }}
                sx={{ flexShrink: 0 }}
              >
                {row.kind === "folder" ? (
                  <FolderOpen fontSize="small" />
                ) : row.kind === "file" &&
                  fileTypeInfo?.supportsInlineView === false ? (
                  <Download fontSize="small" />
                ) : (
                  <OpenInNew fontSize="small" />
                )}
              </IconButton>
            </Tooltip>
          </Stack>
        </ButtonBase>
      );
    },
    [
      activateRow,
      downloadSearchFile,
      getRowText,
      openFileFolder,
      renderPreview,
      rows.length,
      shareSearchFile,
      t,
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
                sm: hasQuery ? 880 : 720,
                lg: hasQuery ? 1040 : 760,
              },
              height: { xs: "100%", sm: hasQuery ? 680 : 112 },
              maxHeight: { xs: "100%", sm: "calc(100vh - 32px)" },
              borderRadius: { xs: 0, sm: 1.5 },
              bgcolor: "background.default",
              transition: theme.transitions.create(["height", "width"], {
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
            minHeight: 0,
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
                    {isMobile ? (
                      <IconButton
                        edge="end"
                        aria-label={tCommon("actions.close")}
                        title={tCommon("actions.close")}
                        onClick={onClose}
                      >
                        <Close />
                      </IconButton>
                    ) : waitingForResults ? (
                      <CircularProgress size={18} />
                    ) : hasSearchQuery ? (
                      <Typography
                        variant="caption"
                        color="text.secondary"
                        noWrap
                        sx={{ px: 0.5 }}
                      >
                        {t("modal.resultsCount", { count: resultCount })}
                      </Typography>
                    ) : null}
                  </InputAdornment>
                ),
              },
            }}
          />

          {error && (
            <Alert severity="error">
              {t("error")}
            </Alert>
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
              {waitingForResults ? (
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
              ) : rows.length === 0 ? (
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
                  <Typography color="text.secondary">
                    {t("noResults")}
                  </Typography>
                </Box>
              ) : (
                <Virtuoso
                  style={{ height: "100%" }}
                  data={rows}
                  overscan={600}
                  defaultItemHeight={68}
                  components={{
                    Scroller: SearchResultsScroller,
                    Footer: () =>
                      loadingMore ? (
                        <Box
                          sx={{
                            display: "flex",
                            justifyContent: "center",
                            py: 1.5,
                          }}
                        >
                          <CircularProgress size={18} />
                        </Box>
                      ) : null,
                  }}
                  computeItemKey={(_, row) => row.id}
                  endReached={loadNextPage}
                  itemContent={renderSearchRow}
                />
              )}
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
