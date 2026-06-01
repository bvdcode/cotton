import { formatBytes } from "../../../shared/utils/formatBytes";
import type { AppTask } from "../../../shared/tasks";

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

    if (task.status === "running" || task.status === "finalizing") {
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

export const calculateTaskStats = (tasks: AppTask[]) => {
  const total = tasks.length;
  let completed = 0;
  let failed = 0;
  let inProgress = 0;
  const activeTasks: AppTask[] = [];

  for (const task of tasks) {
    if (task.status === "completed") {
      completed += 1;
      continue;
    }

    if (task.status === "failed") {
      failed += 1;
      continue;
    }

    if (task.status === "running" || task.status === "finalizing") {
      inProgress += 1;
      activeTasks.push(task);
      continue;
    }

    if (task.status === "queued") {
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
    inProgress,
    activeTasks,
    hasActive,
    allCompleted,
    hasErrors,
  };
};

const shouldShowTaskSpeed = (task: AppTask): boolean =>
  task.kind === "upload" &&
  (task.status === "running" || task.status === "finalizing");

export const getWidgetTitle = (
  stats: ReturnType<typeof calculateTaskStats>,
  isCollapsed: boolean,
  _totalProgress: number,
  totalSpeed: number,
):
  | { key: "titleWithProgress"; options: { completed: number; total: number; speed: string } }
  | { key: "titleWithProgressNoSpeed"; options: { completed: number; total: number } }
  | { key: "titleWithErrors"; options: { total: number; failed: number } }
  | { key: "title" }
  | { key: "titleWithTotal"; options: { total: number } } => {
  const { hasActive, allCompleted, hasErrors, completed, failed, inProgress, total } = stats;

  if (hasActive) {
    const completedCount = Math.min(completed + failed + inProgress, total);
    const hasSpeedTask = stats.activeTasks.some(shouldShowTaskSpeed);

    return hasSpeedTask
      ? {
          key: "titleWithProgress",
          options: {
            completed: completedCount,
            total,
            speed: formatBytes(totalSpeed),
          },
        }
      : {
          key: "titleWithProgressNoSpeed",
          options: { completed: completedCount, total },
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
