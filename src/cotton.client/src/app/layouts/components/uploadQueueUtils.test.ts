import { describe, expect, it } from "vitest";
import type { AppTask } from "../../../shared/tasks";
import { calculateTaskStats, sortTasksByPriority } from "./uploadQueueUtils";

const makeTask = (id: string, status: AppTask["status"]): AppTask => ({
  id,
  kind: "system",
  label: id,
  scopeLabel: "",
  bytesTotal: 100,
  bytesCompleted: status === "completed" ? 100 : 0,
  progress01: status === "completed" ? 1 : 0,
  status,
});

describe("uploadQueueUtils", () => {
  it("prioritizes failed and active generic tasks", () => {
    const sorted = sortTasksByPriority([
      makeTask("completed", "completed"),
      makeTask("queued", "queued"),
      makeTask("running", "running"),
      makeTask("failed", "failed"),
      makeTask("finalizing", "finalizing"),
    ]);

    expect(sorted.map((task) => task.id)).toEqual([
      "failed",
      "running",
      "finalizing",
      "queued",
      "completed",
    ]);
  });

  it("counts running and finalizing tasks as active", () => {
    const stats = calculateTaskStats([
      makeTask("running", "running"),
      makeTask("finalizing", "finalizing"),
      makeTask("queued", "queued"),
      makeTask("completed", "completed"),
    ]);

    expect(stats).toMatchObject({
      total: 4,
      completed: 1,
      failed: 0,
      inProgress: 2,
      hasActive: true,
      allCompleted: false,
    });
    expect(stats.activeTasks.map((task) => task.id)).toEqual([
      "running",
      "finalizing",
      "queued",
    ]);
  });
});
