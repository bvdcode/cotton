import { afterEach, describe, expect, it } from "vitest";
import { UploadManager } from "./UploadManager";

let manager: UploadManager | null = null;

const createManager = () => {
  manager = new UploadManager();
  return manager;
};

describe("UploadManager task facade", () => {
  afterEach(() => {
    manager?.destroy();
    manager = null;
  });

  it("publishes generic tasks alongside upload tasks", () => {
    const taskManager = createManager();
    const handle = taskManager.createTask({
      kind: "encrypt",
      label: "Encrypting video.mov",
      scopeLabel: "Vault",
      bytesTotal: 100,
    });

    expect(taskManager.getSnapshot()).toMatchObject({
      open: true,
      tasks: [
        {
          id: handle.id,
          kind: "encrypt",
          label: "Encrypting video.mov",
          scopeLabel: "Vault",
          bytesTotal: 100,
          bytesCompleted: 0,
          progress01: 0,
          status: "queued",
        },
      ],
    });

    handle.update({
      status: "running",
      bytesCompleted: 25,
      speedBytesPerSec: 50,
    });

    expect(taskManager.getSnapshot().tasks[0]).toMatchObject({
      status: "running",
      bytesCompleted: 25,
      progress01: 0.25,
      speedBytesPerSec: 50,
    });
    expect(taskManager.getSnapshot().overall).toMatchObject({
      bytesTotal: 100,
      bytesCompleted: 25,
      progress01: 0.25,
    });

    handle.complete();

    expect(taskManager.getSnapshot().tasks[0]).toMatchObject({
      status: "completed",
      bytesCompleted: 100,
      progress01: 1,
    });

    taskManager.clearFinished();

    expect(taskManager.getSnapshot()).toMatchObject({
      open: false,
      tasks: [],
    });
  });
});
