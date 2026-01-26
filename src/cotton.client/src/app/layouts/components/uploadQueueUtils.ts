export const formatBytes = (bytes: number): string => {
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

export const sortTasksByPriority = <T extends { status: string }>(
  tasks: T[],
): T[] => {
  const statusPriority: Record<string, number> = {
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

export type UploadTask = {
  id: string;
  status: "uploading" | "finalizing" | "queued" | "completed" | "failed";
  progress01: number;
  fileName: string;
  nodeLabel: string;
  uploadSpeedBytesPerSec?: number | null;
  error?: string;
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
  t: (key: string, options?: Record<string, unknown>) => string,
  stats: ReturnType<typeof calculateUploadStats>,
  isCollapsed: boolean,
  _totalProgress: number,
  totalSpeed: number,
): string => {
  const { hasActive, allCompleted, hasErrors, completed, total } = stats;

  if (hasActive) {
    return t("titleWithProgress", {
      completed: Math.min(completed + 1, total),
      total,
      speed: formatBytes(totalSpeed),
    });
  }

  if (isCollapsed && allCompleted && hasErrors) {
    return t("error");
  }

  if (isCollapsed && allCompleted) {
    return t("title");
  }

  return t("titleWithTotal", { total });
};
