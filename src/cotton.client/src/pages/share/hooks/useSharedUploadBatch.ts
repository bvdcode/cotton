import { useCallback, useEffect, useState } from "react";
import type { AppTask } from "@shared/tasks/types";
import { uploadManager } from "@shared/upload/UploadManager";

export const useSharedUploadBatch = () => {
  const [tasks, setTasks] = useState<AppTask[]>([]);

  useEffect(
    () =>
      uploadManager.subscribe(() => {
        const snapshot = new Map(
          uploadManager.getSnapshot().tasks.map((task) => [task.id, task]),
        );
        setTasks((current) => {
          const updated = current.map((task) => snapshot.get(task.id) ?? task);
          return updated.some((task, index) => task !== current[index])
            ? updated
            : current;
        });
      }),
    [],
  );

  const onQueued = useCallback((ids: string[]) => {
    const selected = new Set(ids);
    const queued = uploadManager
      .getSnapshot()
      .tasks.filter((task) => selected.has(task.id));
    setTasks((current) => {
      const active = current.some(
        (task) => task.status !== "completed" && task.status !== "failed",
      );
      return active ? [...current, ...queued] : queued;
    });
  }, []);

  const reset = useCallback(() => setTasks([]), []);
  const completed = tasks.filter((task) => task.status === "completed").length;
  const failed = tasks.filter((task) => task.status === "failed").length;
  const finished = tasks.length > 0 && completed + failed === tasks.length;
  const total = tasks.reduce((sum, task) => sum + task.bytesTotal, 0);
  const uploaded = tasks.reduce((sum, task) => sum + task.bytesCompleted, 0);
  const progress =
    total > 0 ? uploaded / total : completed / Math.max(tasks.length, 1);

  return { tasks, completed, failed, finished, progress, onQueued, reset };
};
