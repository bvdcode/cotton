import { afterEach, describe, expect, it, vi } from "vitest";
import { UploadManager } from "./UploadManager";

let manager: UploadManager | null = null;
afterEach(() => {
  manager?.destroy();
  vi.useRealTimers();
});

describe("task snapshot publication", () => {
  it("coalesces progress notifications and exposes current immutable snapshots on demand", async () => {
    vi.useFakeTimers();
    manager = new UploadManager();
    const task = manager.createTask({
      kind: "encrypt",
      label: "file",
      bytesTotal: 100,
    });
    const initial = manager.getSnapshot();
    const notify = vi.fn();
    manager.subscribe(notify);
    for (let bytesCompleted = 1; bytesCompleted <= 100; bytesCompleted++) {
      task.update({ bytesCompleted });
    }
    expect(notify).not.toHaveBeenCalled();
    const current = manager.getSnapshot();
    expect(current.tasks[0].bytesCompleted).toBe(100);
    expect(initial.tasks[0].bytesCompleted).toBe(0);
    expect(manager.getSnapshot()).toBe(current);
    await vi.advanceTimersByTimeAsync(100);
    expect(notify).toHaveBeenCalledOnce();
    task.complete();
    expect(manager.getSnapshot().tasks[0].status).toBe("completed");
    await vi.advanceTimersByTimeAsync(100);
    expect(notify).toHaveBeenCalledTimes(2);
  });

  it("publishes explicit visibility changes immediately and cancels pending notifications on disposal", async () => {
    vi.useFakeTimers();
    manager = new UploadManager();
    const task = manager.createTask({
      kind: "encrypt",
      label: "file",
      bytesTotal: 100,
    });
    const notify = vi.fn();
    manager.subscribe(notify);
    task.update({ bytesCompleted: 10 });
    manager.setOpen(false);
    expect(notify).toHaveBeenCalledOnce();
    expect(manager.getSnapshot().open).toBe(false);
    task.update({ bytesCompleted: 20 });
    manager.destroy();
    manager = null;
    await vi.advanceTimersByTimeAsync(1000);
    expect(notify).toHaveBeenCalledOnce();
  });
});
