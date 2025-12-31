import { Box, IconButton, LinearProgress, Paper, Typography } from "@mui/material";
import { Close } from "@mui/icons-material";
import { useCallback, useSyncExternalStore } from "react";
import { uploadManager } from "../../../shared/upload/UploadManager";

const formatBytes = (bytes: number): string => {
  if (!Number.isFinite(bytes) || bytes <= 0) return "0 B";
  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = bytes;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }
  const precision = unitIndex === 0 ? 0 : value < 10 ? 2 : 1;
  return `${value.toFixed(precision)} ${units[unitIndex]}`;
};

export const UploadQueueWidget = () => {
  const subscribe = useCallback((cb: () => void) => uploadManager.subscribe(cb), []);
  const getSnapshot = useCallback(() => uploadManager.getSnapshot(), []);
  const snapshot = useSyncExternalStore(
    subscribe,
    getSnapshot,
    getSnapshot,
  );

  const tasks = snapshot.tasks;
  const hasActive = tasks.some((t) => t.status === "queued" || t.status === "uploading" || t.status === "finalizing");
  const visible = snapshot.open || hasActive;

  if (!visible) return null;

  return (
    <Box
      sx={{
        position: "fixed",
        right: 16,
        bottom: 16,
        zIndex: (theme) => theme.zIndex.snackbar,
        width: 360,
        maxWidth: "calc(100vw - 32px)",
      }}
    >
      <Paper
        elevation={8}
        sx={{
          p: 1.5,
          borderRadius: 2,
        }}
      >
        <Box display="flex" alignItems="center" justifyContent="space-between" mb={1}>
          <Typography variant="subtitle1" fontWeight={600}>
            Uploads
          </Typography>
          <IconButton
            size="small"
            onClick={() => uploadManager.setOpen(false)}
            disabled={hasActive}
            aria-label="Close"
          >
            <Close fontSize="small" />
          </IconButton>
        </Box>

        <Box 
          sx={{
            maxHeight: '400px',
            overflowY: 'auto',
            overflowX: 'hidden',
            display: 'flex',
            flexDirection: 'column',
            gap: 1,
            pr: 0.5,
            '&::-webkit-scrollbar': {
              width: '8px',
            },
            '&::-webkit-scrollbar-track': {
              bgcolor: 'action.hover',
              borderRadius: '4px',
            },
            '&::-webkit-scrollbar-thumb': {
              bgcolor: 'action.selected',
              borderRadius: '4px',
              '&:hover': {
                bgcolor: 'action.disabled',
              },
            },
          }}
        >
          {tasks.map((t) => (
            <Box key={t.id}>
              <Box display="flex" justifyContent="space-between" gap={1}>
                <Typography variant="body2" noWrap title={t.fileName}>
                  {t.fileName}
                </Typography>
                <Typography variant="caption" color="text.secondary" noWrap title={t.nodeLabel}>
                  {t.nodeLabel}
                </Typography>
              </Box>

              <LinearProgress
                variant="determinate"
                value={Math.round(t.progress01 * 100)}
                sx={{ mt: 0.5 }}
              />

              <Box display="flex" justifyContent="space-between" mt={0.5}>
                <Typography variant="caption" color="text.secondary">
                  {t.status === "uploading" && t.uploadSpeedBytesPerSec 
                    ? `${formatBytes(t.uploadSpeedBytesPerSec)}/s`
                    : t.status}
                </Typography>
                <Typography variant="caption" color={t.status === "failed" ? "error" : "text.secondary"}>
                  {t.status === "failed" ? t.error ?? "Failed" : `${Math.round(t.progress01 * 100)}%`}
                </Typography>
              </Box>
            </Box>
          ))}
        </Box>
      </Paper>
    </Box>
  );
};
