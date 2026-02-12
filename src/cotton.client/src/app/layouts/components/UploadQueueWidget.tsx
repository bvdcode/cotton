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
    stats,
    isCollapsed,
    totalProgress,
    totalSpeed,
  );

  const widgetTitleText =
    "options" in widgetTitle
      ? t(widgetTitle.key, widgetTitle.options)
      : t(widgetTitle.key);

  if (!visible) return null;

  return (
    <Box
      sx={{
        position: "fixed",
        left: { xs: "50%", sm: "unset" },
        right: { xs: "unset", sm: 16 },
        transform: { xs: "translateX(-50%)", sm: "none" },
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
          title={widgetTitleText}
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
            pb: isCollapsed ? 0 : 1.5,
          }}
        >
          {tasks.length > 0 && (
            <Virtuoso
              style={{ height: Math.min(tasks.length * 90, 400) }}
              totalCount={tasks.length}
              overscan={5}
              itemContent={(index) => (
                <Box px={1.5}>
                  <UploadTaskRow task={tasks[index]} showDivider={index > 0} />
                </Box>
              )}
            />
          )}
        </Box>
      </Paper>
    </Box>
  );
};
