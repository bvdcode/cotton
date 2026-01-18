import { useState, useEffect } from "react";
import {
  Box,
  CircularProgress,
  Alert,
  Button,
  Stack,
  Paper,
  Typography,
} from "@mui/material";
import MDEditor from "@uiw/react-md-editor";
import EditIcon from "@mui/icons-material/Edit";
import SaveIcon from "@mui/icons-material/Save";
import CancelIcon from "@mui/icons-material/Cancel";
import { useTranslation } from "react-i18next";
import type { Guid } from "../../../../shared/api/layoutsApi";
import { filesApi } from "../../../../shared/api/filesApi";
import { chunksApi } from "../../../../shared/api/chunksApi";
import {
  createIncrementalHasher,
  hashBytes,
  toWebCryptoAlgorithm,
} from "../../../../shared/upload/hash/hashing";
import { uploadConfig } from "../../../../shared/upload/config";
import { useServerSettings } from "../../../../shared/store/useServerSettings";
import { useTheme } from "../../../../app/providers/useTheme";

interface TextPreviewProps {
  nodeFileId: Guid;
  fileName: string;
}

export function TextPreview({ nodeFileId, fileName }: TextPreviewProps) {
  const { t } = useTranslation();
  const { mode } = useTheme();
  const { data: serverSettings } = useServerSettings();
  const [content, setContent] = useState<string | undefined>(undefined);
  const [originalContent, setOriginalContent] = useState<string>("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;

    const loadContent = async () => {
      try {
        setLoading(true);
        setError(null);

        const downloadUrl = await filesApi.getDownloadLink(nodeFileId);
        const response = await fetch(downloadUrl);

        if (!response.ok) {
          throw new Error(
            t("files.preview.errors.loadFailed", {
              error: response.statusText,
            }),
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
              : t("files.preview.errors.loadFailed", { error: "" }),
          );
          setLoading(false);
        }
      }
    };

    loadContent();

    return () => {
      cancelled = true;
    };
  }, [nodeFileId, t]);

  const handleSave = async () => {
    if (!content || content === originalContent || !serverSettings) return;

    try {
      setSaving(true);
      setError(null);

      // Convert content to blob
      const blob = new Blob([content], { type: "text/plain" });
      const arrayBuffer = await blob.arrayBuffer();
      const bytes = new Uint8Array(arrayBuffer);

      // Use server settings for chunking
      const chunkSize = Math.max(1, serverSettings.maxChunkSizeBytes);
      const algorithm = toWebCryptoAlgorithm(
        serverSettings.supportedHashAlgorithm,
      );
      const sendChunkHash = uploadConfig.sendChunkHashForValidation;

      const chunkCount = Math.ceil(bytes.length / chunkSize);
      const chunkHashesByIndex: string[] = new Array(chunkCount);

      // Compute whole-file hash while processing chunks
      const fileHasher = await createIncrementalHasher(algorithm);

      for (let index = 0; index < chunkCount; index += 1) {
        const start = index * chunkSize;
        const end = Math.min(bytes.length, start + chunkSize);
        const chunkBytes = bytes.slice(start, end);
        const chunk = new Blob([chunkBytes], { type: "text/plain" });

        // Update file hasher and compute chunk hash
        fileHasher.update(chunkBytes);
        const chunkHash = await hashBytes(chunkBytes, algorithm);
        chunkHashesByIndex[index] = chunkHash;

        // Upload chunk if needed
        if (sendChunkHash) {
          const exists = await chunksApi.exists(chunkHash);
          if (!exists) {
            await chunksApi.uploadChunk({
              blob: chunk,
              fileName,
              hash: chunkHash,
            });
          }
        } else {
          await chunksApi.uploadChunk({
            blob: chunk,
            fileName,
            hash: null,
          });
        }
      }

      const fileHash = fileHasher.digestHex();

      await filesApi.updateFileContent(nodeFileId, {
        chunkHashes: chunkHashesByIndex,
        hash: fileHash,
        contentType: "text/plain",
        name: fileName,
        nodeId: nodeFileId,
      });

      setOriginalContent(content);
      setIsEditing(false);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : t("files.preview.errors.saveFailed"),
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
    return (
      <Box p={3}>
        <Alert severity="error">{error}</Alert>
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
          py: 1,
          borderRadius: "10px 10px 0 0",
        }}
      >
        <Stack direction="row" spacing={2} alignItems="center" sx={{ mr: 5 }}>
          <Typography variant="subtitle2" sx={{ flexGrow: 1 }}>
            {fileName}
          </Typography>
          {!isEditing && (
            <Button
              size="small"
              startIcon={<EditIcon />}
              onClick={() => setIsEditing(true)}
            >
              {t("files.preview.actions.edit")}
            </Button>
          )}
          {isEditing && (
            <>
              <Button
                size="small"
                startIcon={<CancelIcon />}
                onClick={handleCancel}
                disabled={saving}
              >
                {t("common.actions.cancel")}
              </Button>
              <Button
                size="small"
                variant="contained"
                startIcon={<SaveIcon />}
                onClick={handleSave}
                disabled={!hasChanges || saving}
              >
                {saving
                  ? t("files.preview.actions.saving")
                  : t("files.preview.actions.save")}
              </Button>
            </>
          )}
        </Stack>
      </Paper>

      <Box sx={{ flexGrow: 1, overflow: "auto" }} data-color-mode={mode}>
        <MDEditor
          value={content}
          onChange={setContent}
          preview={isEditing ? "edit" : "preview"}
          hideToolbar={!isEditing}
          height="100%"
          visibleDragbar={false}
        />
      </Box>
    </Box>
  );
}
