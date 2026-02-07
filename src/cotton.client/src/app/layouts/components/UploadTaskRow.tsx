import React from "react";
import { Box, Divider, LinearProgress, Typography } from "@mui/material";
import { formatBytes, type UploadTask } from "./uploadQueueUtils";

interface UploadTaskRowProps {
  task: UploadTask;
  t: (key: string) => string;
  showDivider: boolean;
}

export const UploadTaskRow: React.FC<UploadTaskRowProps> = ({
  task,
  t,
  showDivider,
}) => {
  const isFailed = task.status === "failed";

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
            ? (task.error ?? t("status.failed"))
            : `${Math.round(task.progress01 * 100)}%`}
        </Typography>
      </Box>
    </Box>
  );
};
