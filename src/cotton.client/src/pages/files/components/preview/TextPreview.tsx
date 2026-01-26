/**
 * Text Preview Component
 * 
 * Orchestrator component that manages text file preview and editing
 * Following SOLID principles:
 * - Single Responsibility: Coordinates file loading, saving, and editor rendering
 * - Open/Closed: Extensible through editor mode system
 * - Dependency Inversion: Depends on editor abstractions
 */

import { useState, useEffect } from "react";
import {
  Box,
  CircularProgress,
  Alert,
  Button,
  IconButton,
  Stack,
  Paper,
  Tooltip,
  Typography,
  useMediaQuery,
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import SaveIcon from "@mui/icons-material/Save";
import CancelIcon from "@mui/icons-material/Cancel";
import { useTranslation } from "react-i18next";
import type { Guid } from "../../../../shared/api/layoutsApi";
import { filesApi } from "../../../../shared/api/filesApi";
import { uploadBlobToChunks } from "../../../../shared/upload";
import { useServerSettings } from "../../../../shared/store/useServerSettings";
import { useTheme as useMuiTheme } from "@mui/material/styles";
import { EditorFactory } from "./factories/EditorFactory";
import { EditorModeSelector, LanguageSelector } from "./editors";
import { useEditorMode } from "./hooks/useEditorMode";
import { useLanguageSelection } from "./hooks/useLanguageSelection";
import { EditorMode } from "./editors/types";

const MAX_PREVIEW_SIZE_BYTES = 512 * 1024; // 512 KB - Monaco/MDEditor freeze on larger files

interface TextPreviewProps {
  nodeFileId: Guid;
  fileName: string;
  fileSizeBytes: number | null;
  onSaved?: () => void;
}

export function TextPreview({
  nodeFileId,
  fileName,
  fileSizeBytes,
  onSaved,
}: TextPreviewProps) {
  const { t } = useTranslation(["files", "common"]);
  const muiTheme = useMuiTheme();
  const isMobile = useMediaQuery(muiTheme.breakpoints.down("sm"));
  const { data: serverSettings } = useServerSettings();
  const [content, setContent] = useState<string | undefined>(undefined);
  const [originalContent, setOriginalContent] = useState<string>("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [saving, setSaving] = useState(false);

  // Use editor mode hook for smart mode management
  const { mode, setMode } = useEditorMode({
    content: content || '',
    fileName,
    fileId: nodeFileId,
    fileSize: fileSizeBytes ?? undefined,
  });

  // Use language selection hook for code editor
  const { language, setLanguage } = useLanguageSelection({
    fileName,
    fileId: nodeFileId,
  });

  useEffect(() => {
    let cancelled = false;

    const loadContent = async () => {
      try {
        setLoading(true);
        setError(null);

        // Check file size before loading
        if (fileSizeBytes && fileSizeBytes > MAX_PREVIEW_SIZE_BYTES) {
          if (!cancelled) {
            const sizeMB = fileSizeBytes / 1024 / 1024;
            const maxMB = MAX_PREVIEW_SIZE_BYTES / 1024;
            setError(
              t("preview.errors.fileTooLarge", {
                ns: "files",
                size: sizeMB >= 1 ? `${Math.round(sizeMB)} MB` : `${Math.round(fileSizeBytes / 1024)} KB`,
                maxSize: `${maxMB} KB`,
              })
            );
            setLoading(false);
          }
          return;
        }

        const downloadUrl = await filesApi.getDownloadLink(nodeFileId);
        const response = await fetch(downloadUrl);

        if (!response.ok) {
          const errorSuffix = response.statusText ? `: ${response.statusText}` : "";
          throw new Error(
            t("preview.errors.loadFailed", { ns: "files", error: errorSuffix }),
          );
        }

        const text = await response.text();
        if (!cancelled) {
          setContent(text);
          setOriginalContent(text);
          setLoading(false);
        }
      } catch (err) {
        if (!cancelled) {
          setError(
            err instanceof Error
              ? err.message
              : t("preview.errors.loadFailed", { ns: "files", error: "" }),
          );
          setLoading(false);
        }
      }
    };

    loadContent();

    return () => {
      cancelled = true;
    };
  }, [nodeFileId, fileSizeBytes, t]);

  const handleSave = async () => {
    if (!content || content === originalContent || !serverSettings) return;

    try {
      setSaving(true);
      setError(null);

      // Convert content to blob and reuse shared chunk uploader.
      const blob = new Blob([content], { type: "text/plain" });
      const { chunkHashes, fileHash } = await uploadBlobToChunks({
        blob,
        fileName,
        server: {
          maxChunkSizeBytes: serverSettings.maxChunkSizeBytes,
          supportedHashAlgorithm: serverSettings.supportedHashAlgorithm,
        },
      });

      await filesApi.updateFileContent(nodeFileId, {
        chunkHashes,
        hash: fileHash,
        contentType: "text/plain",
        name: fileName,
        nodeId: nodeFileId,
      });

      setOriginalContent(content);
      setIsEditing(false);
      onSaved?.();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : t("preview.errors.saveFailed", { ns: "files" }),
      );
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    setContent(originalContent);
    setIsEditing(false);
  };

  if (loading) {
    return (
      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        minHeight={400}
      >
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    const isFileTooLarge = fileSizeBytes && fileSizeBytes > MAX_PREVIEW_SIZE_BYTES;
    return (
      <Box p={3}>
        <Alert 
          severity="error"
          action={
            isFileTooLarge ? (
              <Button 
                color="inherit" 
                size="small"
                onClick={() => {
                  void filesApi.getDownloadLink(nodeFileId).then(url => {
                    const link = document.createElement('a');
                    link.href = url;
                    link.download = fileName;
                    link.click();
                  });
                }}
              >
                {t('common:actions.download')}
              </Button>
            ) : undefined
          }
        >
          {error}
        </Alert>
      </Box>
    );
  }

  const hasChanges = content !== originalContent;

  return (
    <Box sx={{ height: "100%", display: "flex", flexDirection: "column" }}>
      <Paper
        elevation={0}
        sx={{
          borderBottom: 1,
          borderColor: "divider",
          px: 2,
          py: 1.5,
          borderRadius: {
            xs: "0px 0px 0 0",
            sm: "10px 10px 0 0",
          },
        }}
      >
        <Stack direction="row" spacing={2} alignItems="center" sx={{ mr: 5 }}>
          <Typography variant="subtitle2" sx={{ flexGrow: 1 }}>
            {fileName}
          </Typography>
          
          {/* Editor Mode Selector */}
          <EditorModeSelector
            currentMode={mode}
            onModeChange={setMode}
            disabled={saving}
          />
          
          {/* Language Selector - only visible in Code mode */}
          {mode === EditorMode.Code && (
            <LanguageSelector
              currentLanguage={language}
              onLanguageChange={setLanguage}
              disabled={saving}
            />
          )}
          
          {/* Edit/Save/Cancel buttons */}
          {!isEditing &&
            (isMobile ? (
              <Tooltip title={t("preview.actions.edit", { ns: "files" })}>
                <span>
                  <IconButton
                    size="small"
                    onClick={() => setIsEditing(true)}
                    aria-label={t("preview.actions.edit", { ns: "files" })}
                  >
                    <EditIcon fontSize="small" />
                  </IconButton>
                </span>
              </Tooltip>
            ) : (
              <Button
                size="small"
                startIcon={<EditIcon />}
                onClick={() => setIsEditing(true)}
              >
                {t("preview.actions.edit", { ns: "files" })}
              </Button>
            ))}
          {isEditing && (
            <>
              {isMobile ? (
                <>
                  <Tooltip title={t("actions.cancel", { ns: "common" })}>
                    <span>
                      <IconButton
                        size="small"
                        onClick={handleCancel}
                        disabled={saving}
                        aria-label={t("actions.cancel", { ns: "common" })}
                      >
                        <CancelIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>
                  <Tooltip title={t("preview.actions.save", { ns: "files" })}>
                    <span>
                      <IconButton
                        size="small"
                        onClick={handleSave}
                        disabled={!hasChanges || saving}
                        aria-label={t("preview.actions.save", { ns: "files" })}
                      >
                        <SaveIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>
                </>
              ) : (
                <>
                  <Button
                    size="small"
                    startIcon={<CancelIcon />}
                    onClick={handleCancel}
                    disabled={saving}
                  >
                    {t("actions.cancel", { ns: "common" })}
                  </Button>
                  <Button
                    size="small"
                    variant="contained"
                    startIcon={<SaveIcon />}
                    onClick={handleSave}
                    disabled={!hasChanges || saving}
                  >
                    {saving
                      ? t("preview.actions.saving", { ns: "files" })
                      : t("preview.actions.save", { ns: "files" })}
                  </Button>
                </>
              )}
            </>
          )}
        </Stack>
      </Paper>

      <Box sx={{ flexGrow: 1, overflow: "auto" }}>
        <EditorFactory
          mode={mode}
          value={content}
          onChange={setContent}
          isEditing={isEditing}
          fileName={fileName}
          language={mode === EditorMode.Code ? language : undefined}
        />
      </Box>
    </Box>
  );
}
