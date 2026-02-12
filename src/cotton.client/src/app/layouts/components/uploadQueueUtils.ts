import { formatBytes } from "../../../shared/utils/formatBytes";

export const sortTasksByPriority = <T extends { status: string }>(
  tasks: T[],
): T[] => {
  const statusPriority: Record<string, number> = {
    failed: 0,
    uploading: 1,
    finalizing: 1,
    queued: 2,
    completed: 3,
  };

  return [...tasks].sort((a, b) => {
    const priorityA = statusPriority[a.status];
    const priorityB = statusPriority[b.status];
    return priorityA - priorityB;
  });
};

export type UploadTask = {
  id: string;
  status: "uploading" | "finalizing" | "queued" | "completed" | "failed";
  progress01: number;
  fileName: string;
  nodeLabel: string;
  uploadSpeedBytesPerSec?: number | null;
  error?: string;
  errorKey?: string;
  completedAt?: number;
};

export const calculateUploadStats = (tasks: UploadTask[]) => {
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

  return {
    total,
    completed,
    failed,
    activeTasks,
    hasActive,
    allCompleted,
    hasErrors,
  };
};

export const getWidgetTitle = (
  stats: ReturnType<typeof calculateUploadStats>,
  isCollapsed: boolean,
  _totalProgress: number,
  totalSpeed: number,
):
  | { key: "titleWithProgress"; options: { completed: number; total: number; speed: string } }
  | { key: "titleWithErrors"; options: { total: number; failed: number } }
  | { key: "title" }
  | { key: "titleWithTotal"; options: { total: number } } => {
  const { hasActive, allCompleted, hasErrors, completed, failed, total } = stats;

  if (hasActive) {
    return {
      key: "titleWithProgress",
      options: {
      completed: Math.min(completed + 1, total),
      total,
      speed: formatBytes(totalSpeed),
      },
    };
  }

  if (allCompleted && hasErrors) {
    return { key: "titleWithErrors", options: { total, failed } };
  }

  if (isCollapsed && allCompleted) {
    return { key: "title" };
  }

  return { key: "titleWithTotal", options: { total } };
};
