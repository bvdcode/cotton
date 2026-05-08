import React from "react";
import { Box, Divider, LinearProgress, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { formatBytes } from "../../../shared/utils/formatBytes";
import type { UploadTask } from "./uploadQueueUtils";

interface UploadTaskRowProps {
  task: UploadTask;
  showDivider: boolean;
}

export const UploadTaskRow: React.FC<UploadTaskRowProps> = ({
  task,
  showDivider,
}) => {
  const { t } = useTranslation("upload");
  const isFailed = task.status === "failed";
  const progressPercent =
    task.status === "completed"
      ? 100
      : Math.min(99, Math.floor(task.progress01 * 100));

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
        value={progressPercent}
        color={isFailed ? "error" : "primary"}
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
          color={isFailed ? "error" : "text.secondary"}
        >
          {isFailed
            ? (task.errorKey ? t(`errors.${task.errorKey}`) : task.error ?? t("status.failed"))
            : `${progressPercent}%`}
        </Typography>
      </Box>
    </Box>
  );
};
