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
import type { Guid } from "../../../../shared/api/layoutsApi";
import { filesApi } from "../../../../shared/api/filesApi";
import { chunksApi } from "../../../../shared/api/chunksApi";
import { hashBytes } from "../../../../shared/upload/hash/hashing";

interface TextPreviewProps {
  nodeFileId: Guid;
  fileName: string;
}

export function TextPreview({ nodeFileId, fileName }: TextPreviewProps) {
  const [content, setContent] = useState<string | undefined>(undefined);
  const [originalContent, setOriginalContent] = useState<string>("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  // TODO: Backend must expose fileManifestId in NodeFileManifestDto response
  // This is required for the update-content endpoint to validate we're editing
  // the correct version of the file
  const [fileManifestId, setFileManifestId] = useState<Guid>("");

  const isMarkdown =
    fileName.toLowerCase().endsWith(".md") ||
    fileName.toLowerCase().endsWith(".markdown");

  useEffect(() => {
    let cancelled = false;

    const loadContent = async () => {
      try {
        setLoading(true);
        setError(null);

        const downloadUrl = await filesApi.getDownloadLink(nodeFileId);
        const response = await fetch(downloadUrl);

        if (!response.ok) {
          throw new Error(`Failed to load file: ${response.statusText}`);
        }

        const text = await response.text();

        // TODO: Get fileManifestId from API response
        // For now, we'll use nodeFileId as a placeholder
        // Backend needs to add fileManifestId to NodeFileManifestDto
        setFileManifestId(nodeFileId);

        if (!cancelled) {
          setContent(text);
          setOriginalContent(text);
          setLoading(false);
        }
      } catch (err) {
        if (!cancelled) {
          setError(
            err instanceof Error ? err.message : "Failed to load file content",
          );
          setLoading(false);
        }
      }
    };

    loadContent();

    return () => {
      cancelled = true;
    };
  }, [nodeFileId]);

  const handleSave = async () => {
    if (!content || content === originalContent) return;

    try {
      setSaving(true);
      setError(null);

      // Convert content to blob
      const blob = new Blob([content], { type: "text/plain" });
      const arrayBuffer = await blob.arrayBuffer();
      const uint8Array = new Uint8Array(arrayBuffer);

      // Hash the entire content
      const contentHash = await hashBytes(uint8Array, "SHA-256");

      // For simplicity, upload as single chunk
      // In production, you might want to chunk large files
      const chunkHash = contentHash;

      // Check if chunk exists
      const exists = await chunksApi.exists(chunkHash);
      if (!exists) {
        await chunksApi.uploadChunk({
          blob,
          fileName,
          hash: chunkHash,
        });
      }

      // TODO: The backend needs to expose fileManifestId in NodeFileManifestDto
      // For now, we use nodeFileId as baseManifestId - this will likely fail
      // backend validation. The backend should either:
      // 1. Add fileManifestId to NodeFileManifestDto, OR
      // 2. Make baseManifestId optional and auto-fetch from nodeFileId
      await filesApi.updateFileContent(nodeFileId, {
        chunkHashes: [chunkHash],
        hash: contentHash,
        baseManifestId: fileManifestId,
      });

      setOriginalContent(content);
      setIsEditing(false);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to save file content",
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
        <Stack direction="row" spacing={2} alignItems="center">
          <Typography variant="subtitle2" sx={{ flexGrow: 1 }}>
            {fileName}
          </Typography>
          {!isEditing && (
            <Button
              size="small"
              startIcon={<EditIcon />}
              onClick={() => setIsEditing(true)}
            >
              Edit
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
                Cancel
              </Button>
              <Button
                size="small"
                variant="contained"
                startIcon={<SaveIcon />}
                onClick={handleSave}
                disabled={!hasChanges || saving}
              >
                {saving ? "Saving..." : "Save"}
              </Button>
            </>
          )}
        </Stack>
      </Paper>

      <Box sx={{ flexGrow: 1, overflow: "auto", p: 2 }} data-color-mode="light">
        {isMarkdown ? (
          <MDEditor
            value={content}
            onChange={setContent}
            preview={isEditing ? "edit" : "preview"}
            hideToolbar={!isEditing}
            height="100%"
            visibleDragbar={false}
          />
        ) : (
          <Paper
            variant="outlined"
            sx={{
              p: 2,
              height: "100%",
              overflow: "auto",
              fontFamily: "monospace",
              fontSize: "14px",
              whiteSpace: "pre-wrap",
              backgroundColor: isEditing ? "background.paper" : "grey.50",
            }}
          >
            {isEditing ? (
              <textarea
                value={content}
                onChange={(e) => setContent(e.target.value)}
                style={{
                  width: "100%",
                  height: "100%",
                  border: "none",
                  outline: "none",
                  fontFamily: "inherit",
                  fontSize: "inherit",
                  resize: "none",
                  backgroundColor: "transparent",
                }}
              />
            ) : (
              content
            )}
          </Paper>
        )}
      </Box>
    </Box>
  );
}
