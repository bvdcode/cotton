import { Box, IconButton, LinearProgress, Paper, Typography } from "@mui/material";
import { Close } from "@mui/icons-material";
import { useCallback, useSyncExternalStore } from "react";
import { uploadManager } from "../../../shared/upload/UploadManager";

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

        <Box display="flex" flexDirection="column" gap={1}>
          {tasks.slice(0, 6).map((t) => (
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
                  {t.status}
                </Typography>
                <Typography variant="caption" color={t.status === "failed" ? "error" : "text.secondary"}>
                  {t.status === "failed" ? t.error ?? "Failed" : `${Math.round(t.progress01 * 100)}%`}
                </Typography>
              </Box>
            </Box>
          ))}
        </Box>

        {tasks.length > 6 && (
          <Typography variant="caption" color="text.secondary" display="block" mt={1}>
            +{tasks.length - 6} more
          </Typography>
        )}
      </Paper>
    </Box>
  );
};
