import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Alert,
  Box,
  ButtonBase,
  CircularProgress,
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  InputAdornment,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import { alpha } from "@mui/material/styles";
import {
  Article,
  Close,
  Folder,
  FolderOpen,
  Image as ImageIcon,
  InsertDriveFile,
  OpenInNew,
  Search,
  Settings,
  TextSnippet,
  VideoFile,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { UserRole } from "../auth/types";
import { useAuthStore } from "../../shared/store/authStore";
import { useLayoutsStore } from "../../shared/store/layoutsStore";
import { useSearchFileList } from "../../shared/hooks/useFileListSource";
import { formatBytes } from "../../shared/utils/formatBytes";
import type { NodeDto } from "../../shared/api/layoutsApi";
import type { NodeFileManifestDto } from "../../shared/api/nodesApi";
import { useLayoutSearch } from "../../pages/search/hooks/useLayoutSearch";
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

const normalizeSearchText = (value: string): string =>
  value
    .toLocaleLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "");

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

export const SearchModal = ({ open, onClose }: SearchModalProps) => {
  const { t } = useTranslation(["search", "files", "common"]);
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement | null>(null);
  const userRole = useAuthStore((s) => s.user?.role ?? null);
  const { rootNode, ensureHomeData } = useLayoutsStore();
  const [failedPreviews, setFailedPreviews] = useState<Set<string>>(new Set());

  const layoutId = rootNode?.layoutId;

  const searchState = useLayoutSearch({
    layoutId,
    debounceMs: 160,
    pageSize: 40,
  });

  const { query, totalCount, loading, error, results, setQuery, setPage } =
    searchState;
  const hasQuery = query.trim().length > 0;

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
    setPage(1);
  }, [query, setPage]);

  const rawDictionary = t("dictionary", {
    ns: "search",
    returnObjects: true,
    defaultValue: [],
  }) as unknown;

  const dictionaryEntries = useMemo(() => {
    const entries = Array.isArray(rawDictionary)
      ? rawDictionary.filter(isDictionaryEntry)
      : [];

    return entries.filter(
      (entry) => !entry.adminOnly || userRole === UserRole.Admin,
    );
  }, [rawDictionary, userRole]);

  const matchedDictionaryRows = useMemo(() => {
    const normalizedQuery = normalizeSearchText(query.trim());
    if (!normalizedQuery) return [];

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

        if (!haystack.includes(normalizedQuery)) return null;

        const keywordStarts = normalizedKeywords.some((keyword) =>
          keyword.startsWith(normalizedQuery),
        );
        const score = normalizedTitle.startsWith(normalizedQuery)
          ? 0
          : keywordStarts
            ? 1
            : normalizedTitle.includes(normalizedQuery)
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
  }, [dictionaryEntries, query]);

  const fileListSource = useSearchFileList({
    results,
    loading,
    error,
    totalCount,
    hasQuery,
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
    () => (hasQuery ? [...matchedDictionaryRows, ...contentRows] : []),
    [contentRows, hasQuery, matchedDictionaryRows],
  );

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

  const openFolder = useCallback(
    (nodeId: string) => {
      navigate(`/files/${nodeId}`);
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

  const renderPreview = (row: SearchRow) => {
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
            width: 36,
            height: 36,
            objectFit: "cover",
            borderRadius: 1,
          }}
        />
      );
    }

    return getSmallFileIcon(row.file.name);
  };

  const getRowText = (row: SearchRow) => {
    if (row.kind === "setting") {
      return {
        title: row.entry.title,
        meta:
          row.entry.description ??
          t("types.setting", { ns: "search", defaultValue: "Setting" }),
        action: t("actions.openSetting", {
          ns: "search",
          defaultValue: "Open settings",
        }),
      };
    }

    if (row.kind === "folder") {
      return {
        title: row.node.name,
        meta:
          row.path ??
          t("types.folder", { ns: "search", defaultValue: "Folder" }),
        action: t("actions.openFolder", {
          ns: "search",
          defaultValue: "Open folder",
        }),
      };
    }

    const size = formatBytes(row.file.sizeBytes);
    return {
      title: row.file.name,
      meta: row.path ? `${row.path} - ${size}` : size,
      action: t("actions.openFile", {
        ns: "search",
        defaultValue: "Open file",
      }),
    };
  };

  const activateRow = (row: SearchRow) => {
    if (row.kind === "setting") {
      openSetting(row.entry);
      return;
    }

    if (row.kind === "folder") {
      openFolder(row.node.id);
      return;
    }

    openFile(row.file);
  };

  const resultCaption = hasQuery
    ? t("modal.resultsCount", {
        ns: "search",
        count: rows.length,
        defaultValue: "{{count}} results",
      })
    : t("modal.emptyQuery", {
        ns: "search",
        defaultValue: "Start typing to search files, folders, and settings.",
      });

  return (
    <>
      <Dialog
        open={open}
        onClose={onClose}
        maxWidth={false}
        slotProps={{
          paper: {
            sx: {
              width: { xs: "calc(100vw - 16px)", sm: 880, lg: 1040 },
              height: { xs: "min(82vh, 680px)", sm: 680 },
              maxHeight: "calc(100vh - 32px)",
              borderRadius: 1.5,
            },
          },
        }}
      >
        <DialogTitle
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: 2,
            pb: 1,
          }}
        >
          <Typography variant="h6" fontWeight={700}>
            {t("modal.title", { ns: "search", defaultValue: "Search" })}
          </Typography>
          <IconButton
            onClick={onClose}
            aria-label={t("common:actions.close")}
            title={t("common:actions.close")}
          >
            <Close />
          </IconButton>
        </DialogTitle>

        <DialogContent
          sx={{
            display: "flex",
            flexDirection: "column",
            gap: 1.5,
            minHeight: 0,
            pt: 1,
          }}
        >
          <TextField
            fullWidth
            autoFocus
            inputRef={inputRef}
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            disabled={!layoutId}
            placeholder={t("modal.placeholder", {
              ns: "search",
              defaultValue: "Search files, folders, settings...",
            })}
            slotProps={{
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <Search color="action" />
                  </InputAdornment>
                ),
                endAdornment: loading ? (
                  <InputAdornment position="end">
                    <CircularProgress size={18} />
                  </InputAdornment>
                ) : undefined,
              },
            }}
          />

          {error && (
            <Alert severity="error">
              {t("error", { ns: "search", defaultValue: "Search failed. Please try again." })}
            </Alert>
          )}

          <Typography variant="caption" color="text.secondary">
            {resultCaption}
          </Typography>

          <Box
            sx={(theme) => ({
              flex: 1,
              minHeight: 0,
              overflowY: "auto",
              border: 1,
              borderColor: "divider",
              borderRadius: 1,
              bgcolor: alpha(theme.palette.background.default, 0.65),
            })}
          >
            {!hasQuery ? (
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
                  {t("enterQueryHint", {
                    ns: "search",
                    defaultValue: "Start typing to search...",
                  })}
                </Typography>
              </Box>
            ) : rows.length === 0 && !loading ? (
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
                  {t("noResults", {
                    ns: "search",
                    defaultValue: "No files or folders found",
                  })}
                </Typography>
              </Box>
            ) : (
              <Stack divider={<Box sx={{ borderBottom: 1, borderColor: "divider" }} />}>
                {rows.map((row) => {
                  const text = getRowText(row);
                  return (
                    <ButtonBase
                      key={row.id}
                      onClick={() => activateRow(row)}
                      sx={(theme) => ({
                        width: "100%",
                        minHeight: 68,
                        justifyContent: "stretch",
                        textAlign: "left",
                        px: 1.25,
                        py: 1,
                        "&:hover": {
                          bgcolor: alpha(theme.palette.primary.main, 0.06),
                        },
                      })}
                    >
                      <Stack
                        direction="row"
                        spacing={1.25}
                        alignItems="center"
                        width="100%"
                        minWidth={0}
                      >
                        <Box
                          sx={(theme) => ({
                            width: 44,
                            height: 44,
                            flexShrink: 0,
                            borderRadius: 1,
                            bgcolor: alpha(theme.palette.text.primary, 0.06),
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                            overflow: "hidden",
                          })}
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

                        <Tooltip title={text.action}>
                          <IconButton
                            size="small"
                            aria-label={text.action}
                            onClick={(event) => {
                              event.stopPropagation();
                              activateRow(row);
                            }}
                            sx={{ flexShrink: 0 }}
                          >
                            {row.kind === "folder" ? (
                              <FolderOpen fontSize="small" />
                            ) : (
                              <OpenInNew fontSize="small" />
                            )}
                          </IconButton>
                        </Tooltip>
                      </Stack>
                    </ButtonBase>
                  );
                })}
              </Stack>
            )}
          </Box>
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
