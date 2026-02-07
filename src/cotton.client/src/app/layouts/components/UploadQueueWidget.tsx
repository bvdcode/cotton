import { useCallback, useState, useSyncExternalStore } from "react";
import { Box, Paper } from "@mui/material";
import { useTranslation } from "react-i18next";
import { Virtuoso } from "react-virtuoso";
import { uploadManager } from "../../../shared/upload/UploadManager";
import { WidgetHeader } from "./WidgetHeader";
import { UploadTaskRow } from "./UploadTaskRow";
import {
  sortTasksByPriority,
  calculateUploadStats,
  getWidgetTitle,
} from "./uploadQueueUtils";

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
  const stats = calculateUploadStats(tasks);
  const totalSpeed = snapshot.overall.uploadSpeedBytesPerSec;
  const visible = snapshot.open && stats.total > 0;

  const totalProgress = Math.max(
    0,
    Math.min(100, snapshot.overall.progress01 * 100),
  );

  const widgetTitle = getWidgetTitle(
    t,
    stats,
    isCollapsed,
    totalProgress,
    totalSpeed,
  );

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
          borderRadius: 2,
          position: "relative",
          overflow: "hidden",
        }}
      >
        <WidgetHeader
          title={widgetTitle}
          isCollapsed={isCollapsed}
          hasActive={stats.hasActive}
          progressPercent={totalProgress}
          onToggleCollapse={() => setIsCollapsed(!isCollapsed)}
          onClose={() => {
            // Close clears the list to avoid keeping stale tasks.
            // Close is hidden while uploads are active.
            uploadManager.clearFinished({ includeCompleted: true, includeFailed: true });
          }}
          aria={{
            expand: t("actions.expand"),
            collapse: t("actions.collapse"),
            close: t("actions.close"),
          }}
        />

        <Box
          sx={{
            maxHeight: isCollapsed
              ? 0
              : `${Math.min(tasks.length * 90, 400)}px`,
            overflow: "hidden",
            position: "relative",
            zIndex: 1,
            transition: "max-height 0.3s ease-in-out, opacity 0.3s ease-in-out",
            opacity: isCollapsed ? 0 : 1,
            px: isCollapsed ? 0 : 1.5,
            pb: isCollapsed ? 0 : 1.5,
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
