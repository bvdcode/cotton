import type { AppTaskStatus } from "../tasks/types";
import type { ExternalTaskInternal } from "./UploadExternalTasks";
import type { UploadTaskInternal } from "./UploadManager";
import { toAppTaskStatus } from "./UploadTaskView";

const MAX_FINISHED_TASKS = 10000;
const FINISHED_TASK_TTL_MS = 30 * 60 * 1000;
const FINISHED_TASK_STATUSES = new Set<AppTaskStatus>(["completed", "failed"]);

export const filterFinishedTasks = <T extends { status: string }>(
  tasks: T[],
  includeCompleted: boolean,
  includeFailed: boolean,
): T[] =>
  tasks.filter((task) => {
    if (task.status === "completed") return !includeCompleted;
    if (task.status === "failed") return !includeFailed;
    return true;
  });

const pruneTasks = <T extends { completedAt?: number; status: string }>(
  tasks: T[],
  status: (task: T) => AppTaskStatus,
): void => {
  const now = Date.now();

  for (let index = tasks.length - 1; index >= 0; index -= 1) {
    const task = tasks[index];
    if (
      FINISHED_TASK_STATUSES.has(status(task)) &&
      task.completedAt &&
      now - task.completedAt > FINISHED_TASK_TTL_MS
    ) {
      tasks.splice(index, 1);
    }
  }

  const finishedCount = tasks.filter((task) =>
    FINISHED_TASK_STATUSES.has(status(task)),
  ).length;
  const toRemove = Math.max(0, finishedCount - MAX_FINISHED_TASKS);
  let removed = 0;

  for (
    let index = tasks.length - 1;
    index >= 0 && removed < toRemove;
    index -= 1
  ) {
    if (FINISHED_TASK_STATUSES.has(status(tasks[index]))) {
      tasks.splice(index, 1);
      removed += 1;
    }
  }
};

export const pruneUploadTasks = (tasks: UploadTaskInternal[]): void =>
  pruneTasks(tasks, (task) => toAppTaskStatus(task.status));

export const pruneExternalTasks = (tasks: ExternalTaskInternal[]): void =>
  pruneTasks(tasks, (task) => task.status);
