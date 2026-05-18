import React from "react";
import { Box, Divider, LinearProgress, Typography } from "@mui/material";
import type { LinearProgressProps } from "@mui/material";
import { useTranslation } from "react-i18next";
import { formatBytes } from "../../../shared/utils/formatBytes";
import type { AppTask } from "../../../shared/tasks";

interface UploadTaskRowProps {
  task: AppTask;
  showDivider: boolean;
}

export const UploadTaskRow: React.FC<UploadTaskRowProps> = ({
  task,
  showDivider,
}) => {
  const { t } = useTranslation("tasks");
  const isFailed = task.status === "failed";
  const progressPercent =
    task.status === "completed"
      ? 100
      : Math.min(99, Math.floor(task.progress01 * 100));
  const progressColor = getTaskProgressColor(task);

  return (
    <Box sx={{ pr: 0.5, pb: 1 }}>
      {showDivider && <Divider sx={{ mb: 1 }} />}
      <Box>
        <Typography
          variant="body2"
          noWrap
          title={task.label}
          sx={{ fontWeight: 500 }}
        >
          {task.label}
        </Typography>
        <Typography
          variant="caption"
          color="text.secondary"
          noWrap
          title={task.scopeLabel}
          sx={{
            display: "block",
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
          }}
        >
          {task.scopeLabel}
        </Typography>
      </Box>

      <LinearProgress
        variant="determinate"
        value={progressPercent}
        color={isFailed ? "error" : progressColor}
        sx={{ mt: 0.5 }}
      />

      <Box display="flex" justifyContent="space-between" mt={0.5}>
        <Typography variant="caption" color="text.secondary">
          {task.status === "running" && task.speedBytesPerSec != null
            ? `${formatBytes(task.speedBytesPerSec)}/s`
            : t(`status.${task.status}`)}
        </Typography>
        <Typography
          variant="caption"
          color={isFailed ? "error" : "text.secondary"}
        >
          {isFailed
            ? (task.errorKey
                ? t(`errors.${task.errorKey}`, task.errorParams)
                : task.error ?? t("status.failed"))
            : `${progressPercent}%`}
        </Typography>
      </Box>
    </Box>
  );
};

const getTaskProgressColor = (
  task: AppTask,
): NonNullable<LinearProgressProps["color"]> => {
  switch (task.kind) {
    case "encrypt":
    case "decrypt":
      return "secondary";
    case "convert":
      return "info";
    case "system":
      return "warning";
    case "upload":
    default:
      return "primary";
  }
};
