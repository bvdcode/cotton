import { formatBytes } from "../../../shared/utils/formatBytes";

export const sortTasksByPriority = <T extends { status: string }>(
  tasks: T[],
): T[] => {
  const failed: T[] = [];
  const active: T[] = [];
  const queued: T[] = [];
  const completed: T[] = [];
  const rest: T[] = [];

  for (const task of tasks) {
    if (task.status === "failed") {
      failed.push(task);
      continue;
    }

    if (task.status === "uploading" || task.status === "finalizing") {
      active.push(task);
      continue;
    }

    if (task.status === "queued") {
      queued.push(task);
      continue;
    }

    if (task.status === "completed") {
      completed.push(task);
      continue;
    }

    rest.push(task);
  }

  return [...failed, ...active, ...queued, ...completed, ...rest];
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
  let completed = 0;
  let failed = 0;
  const activeTasks: UploadTask[] = [];

  for (const task of tasks) {
    if (task.status === "completed") {
      completed += 1;
      continue;
    }

    if (task.status === "failed") {
      failed += 1;
      continue;
    }

    if (
      task.status === "queued" ||
      task.status === "uploading" ||
      task.status === "finalizing"
    ) {
      activeTasks.push(task);
    }
  }

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
