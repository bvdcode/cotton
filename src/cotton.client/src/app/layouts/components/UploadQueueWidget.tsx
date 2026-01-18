import {
  Box,
  Divider,
  IconButton,
  LinearProgress,
  Paper,
  Typography,
} from "@mui/material";
import { Close, ExpandMore, ExpandLess } from "@mui/icons-material";
import { useCallback, useState, useSyncExternalStore } from "react";
import { useTranslation } from "react-i18next";
import { Virtuoso } from "react-virtuoso";
import { uploadManager } from "../../../shared/upload/UploadManager";

const formatBytes = (bytes: number): string => {
  if (!Number.isFinite(bytes) || bytes <= 0) {
    return "0 B";
  }
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

const sortTasksByPriority = (
  tasks: typeof uploadManager extends { getSnapshot(): { tasks: infer T } }
    ? T
    : never[],
) => {
  const statusPriority = {
    uploading: 1,
    finalizing: 1,
    queued: 2,
    completed: 3,
    failed: 4,
  };

  return [...tasks].sort((a, b) => {
    const priorityA = statusPriority[a.status];
    const priorityB = statusPriority[b.status];
    return priorityA - priorityB;
  });
};

type UploadTask = ReturnType<typeof uploadManager.getSnapshot>["tasks"][number];

interface UploadTaskRowProps {
  task: UploadTask;
  t: (key: string) => string;
  showDivider: boolean;
}

const UploadTaskRow = ({ task, t, showDivider }: UploadTaskRowProps) => {
  return (
    <Box sx={{ pr: 0.5, pb: 1 }}>
      {showDivider && <Divider sx={{ mb: 1 }} />}
      <Box>
        <Typography
          variant="body2"
          noWrap
          title={task.fileName}
          sx={{ fontWeight: 500 }}
        >
          {task.fileName}
        </Typography>
        <Typography
          variant="caption"
          color="text.secondary"
          noWrap
          title={task.nodeLabel}
          sx={{
            display: "block",
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
          }}
        >
          {task.nodeLabel}
        </Typography>
      </Box>

      <LinearProgress
        variant="determinate"
        value={Math.round(task.progress01 * 100)}
        sx={{ mt: 0.5 }}
      />

      <Box display="flex" justifyContent="space-between" mt={0.5}>
        <Typography variant="caption" color="text.secondary">
          {task.status === "uploading" && task.uploadSpeedBytesPerSec != null
            ? `${formatBytes(task.uploadSpeedBytesPerSec)}/s`
            : t(`status.${task.status}`)}
        </Typography>
        <Typography
          variant="caption"
          color={task.status === "failed" ? "error" : "text.secondary"}
        >
          {task.status === "failed"
            ? task.error ?? "Failed"
            : `${Math.round(task.progress01 * 100)}%`}
        </Typography>
      </Box>
    </Box>
  );
};

export const UploadQueueWidget = () => {
  const { t } = useTranslation("upload");
  const [isCollapsed, setIsCollapsed] = useState(false);
  const subscribe = useCallback(
    (cb: () => void) => uploadManager.subscribe(cb),
    [],
  );
  const getSnapshot = useCallback(() => uploadManager.getSnapshot(), []);
  const snapshot = useSyncExternalStore(subscribe, getSnapshot, getSnapshot);

  const tasks = sortTasksByPriority(snapshot.tasks);
  const total = tasks.length;
  const completed = tasks.filter((t) => t.status === "completed").length;
  const failed = tasks.filter((t) => t.status === "failed").length;
  const activeTasks = tasks.filter(
    (t) =>
      t.status === "queued" ||
      t.status === "uploading" ||
      t.status === "finalizing",
  );
  const hasActive = activeTasks.length > 0;
  const allCompleted = total > 0 && completed + failed === total;
  const hasErrors = failed > 0;
  const totalSpeed = snapshot.overall.uploadSpeedBytesPerSec;
  const visible = snapshot.open && total > 0;

  const totalProgress = Math.max(0, Math.min(100, snapshot.overall.progress01 * 100));

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
          position: "relative",
          overflow: "hidden",
        }}
      >
        <Box
          display="flex"
          alignItems="center"
          justifyContent="space-between"
          mb={isCollapsed ? 0 : 1}
          sx={{
            position: "relative",
            zIndex: 1,
            borderRadius: 1,
            overflow: "hidden",
          }}
        >
          {/* Battery fill effect in header only (prevents tinting the expanded list) */}
          {hasActive && (
            <Box
              sx={{
                position: "absolute",
                top: 0,
                left: 0,
                bottom: 0,
                width: `${totalProgress}%`,
                bgcolor: "success.main",
                opacity: 0.15,
                transition: "width 0.3s ease-out",
                zIndex: 0,
                pointerEvents: "none",
              }}
            />
          )}
          <Typography
            variant="subtitle1"
            fontWeight={600}
            sx={{
              position: "relative",
              zIndex: 1,
              color:
                isCollapsed && allCompleted && hasErrors
                  ? "error.main"
                  : "text.primary",
            }}
          >
            {hasActive
              ? t("titleWithProgress", {
                  completed,
                  total,
                  speed: formatBytes(totalSpeed),
                })
              : isCollapsed && allCompleted && hasErrors
              ? t("error")
              : isCollapsed && allCompleted
              ? t("title")
              : t("titleWithTotal", { total })}
          </Typography>
          <Box display="flex" gap={0.5} sx={{ position: "relative", zIndex: 1 }}>
            <IconButton
              size="small"
              onClick={() => setIsCollapsed(!isCollapsed)}
              aria-label={isCollapsed ? "Expand" : "Collapse"}
            >
              {isCollapsed ? (
                <ExpandLess fontSize="small" />
              ) : (
                <ExpandMore fontSize="small" />
              )}
            </IconButton>
            <IconButton
              size="small"
              onClick={() => uploadManager.setOpen(false)}
              aria-label="Close"
            >
              <Close fontSize="small" />
            </IconButton>
          </Box>
        </Box>

        <Box
          sx={{
            maxHeight: isCollapsed ? 0 : `${Math.min(tasks.length * 90, 400)}px`,
            overflow: "hidden",
            position: "relative",
            zIndex: 1,
            transition: "max-height 0.3s ease-in-out, opacity 0.3s ease-in-out",
            opacity: isCollapsed ? 0 : 1,
          }}
        >
          {tasks.length > 0 && (
            <Virtuoso
              style={{ height: Math.min(tasks.length * 90, 400) }}
              totalCount={tasks.length}
              overscan={5}
              itemContent={(index) => (
                <UploadTaskRow
                  task={tasks[index]}
                  t={t}
                  showDivider={index > 0}
                />
              )}
            />
          )}
        </Box>
      </Paper>
    </Box>
  );
};
