import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { UploadTaskInternal } from "./UploadManager";
import type { UploadExecutionState } from "./UploadTaskRunner";
import { RollingBytesPerSecondEstimator } from "./RollingBytesPerSecondEstimator";
import { UploadProgressTracker } from "./UploadProgressTracker";

const createTask = (): UploadTaskInternal => ({
  id: "upload-1",
  nodeId: "node-1",
  nodeLabel: "Photos",
  fileName: "photo.jpg",
  bytesTotal: 1024,
  bytesUploaded: 0,
  progress01: 0,
  status: "uploading",
  _encrypt: false,
});

const createState = (): UploadExecutionState => {
  const state: UploadExecutionState = {
    encryptionTask: null,
    encryptionTaskFinished: false,
    lastEmitTime: 0,
    taskEstimator: new RollingBytesPerSecondEstimator(),
  };
  state.taskEstimator.update(0);
  return state;
};

describe("effective upload throughput", () => {
  let now = 1000;

  beforeEach(() => {
    vi.useFakeTimers();
    now = 1000;
    vi.spyOn(Date, "now").mockImplementation(() => now);
  });

  afterEach(() => {
    vi.clearAllTimers();
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("expires frozen speeds without changing progress and resumes on new bytes", () => {
    const emit = vi.fn();
    const tracker = new UploadProgressTracker(() => {}, emit);
    const task = createTask();
    const state = createState();
    tracker.beginBatch();
    tracker.addFile(task.bytesTotal);
    now += 500;
    tracker.record(task, state, 512);
    expect(tracker.getSpeed()).toBeGreaterThan(0);

    for (let elapsed = 0; elapsed < 60000; elapsed += 500) {
      now += 500;
      vi.advanceTimersByTime(500);
    }

    expect(task).toMatchObject({
      bytesUploaded: 512,
      progress01: 0.5,
      uploadSpeedBytesPerSec: 0,
    });
    expect(tracker.getSpeed()).toBe(0);
    expect(emit).toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);

    tracker.record(task, state, 768);
    expect(task.uploadSpeedBytesPerSec).toBeGreaterThan(0);
    expect(tracker.getSpeed()).toBeGreaterThan(0);
    expect(task.bytesUploaded).toBe(768);
    tracker.destroy();
    expect(vi.getTimerCount()).toBe(0);
  });

  it("expires one stalled file while another file keeps uploading", () => {
    const tracker = new UploadProgressTracker(
      () => {},
      () => {},
    );
    const first = createTask();
    const second = { ...createTask(), id: "upload-2", bytesTotal: 4096 };
    const firstState = createState();
    const secondState = createState();
    tracker.beginBatch();
    tracker.addFile(first.bytesTotal);
    tracker.addFile(second.bytesTotal);
    now += 500;
    tracker.record(first, firstState, 512);

    for (let progress = 256; progress <= 2048; progress += 256) {
      now += 500;
      tracker.record(second, secondState, progress);
      vi.advanceTimersByTime(500);
    }

    expect(first.uploadSpeedBytesPerSec).toBe(0);
    expect(first.bytesUploaded).toBe(512);
    expect(second.uploadSpeedBytesPerSec).toBeGreaterThan(0);
    expect(tracker.getSpeed()).toBeGreaterThan(0);
    tracker.destroy();
  });

  it("measures deduplicated bytes and includes the time spent verifying them", () => {
    const tracker = new UploadProgressTracker(
      () => {},
      () => {},
    );
    const task = createTask();
    const state = createState();
    tracker.beginBatch();
    tracker.addFile(task.bytesTotal);

    now += 2000;
    tracker.record(task, state, 1024);

    expect(task.uploadSpeedBytesPerSec).toBe(512);
    expect(tracker.getSpeed()).toBe(512);
    expect(task.progress01).toBe(1);
  });

  it.each(["completed", "failed", "finalizing"] as const)(
    "stops sampling a %s task",
    (status) => {
      const emit = vi.fn();
      const tracker = new UploadProgressTracker(() => {}, emit);
      const task = createTask();
      tracker.beginBatch();
      tracker.addFile(task.bytesTotal);
      tracker.record(task, createState(), 512);
      task.status = status;

      for (let tick = 0; tick < 6; tick += 1) {
        now += 500;
        vi.advanceTimersByTime(500);
      }

      expect(task.uploadSpeedBytesPerSec).toBe(0);
      expect(tracker.getSpeed()).toBe(0);
      expect(vi.getTimerCount()).toBe(0);
      const emissions = emit.mock.calls.length;
      vi.advanceTimersByTime(60000);
      expect(emit).toHaveBeenCalledTimes(emissions);
    },
  );

  it("does not count rolled-back progress twice after an interrupted request", () => {
    const tracker = new UploadProgressTracker(
      () => {},
      () => {},
    );
    const task = createTask();
    const state = createState();
    tracker.beginBatch();
    tracker.addFile(task.bytesTotal);

    now += 500;
    tracker.record(task, state, 512);
    now += 100;
    tracker.record(task, state, 0);
    tracker.record(task, state, 512);
    now += 400;
    tracker.record(task, state, 1024);

    expect(task._bytesProcessedForSpeed).toBe(1024);
    expect(tracker.getSpeed()).toBe(1024);
    expect(task.uploadSpeedBytesPerSec).toBe(1024);
  });

  it("combines progress across files without counting completed files again", () => {
    const tracker = new UploadProgressTracker(
      () => {},
      () => {},
    );
    const first = createTask();
    const second = { ...createTask(), id: "upload-2" };
    const firstState = createState();
    const secondState = createState();
    tracker.beginBatch();
    tracker.addFile(first.bytesTotal);
    tracker.addFile(second.bytesTotal);

    now += 1000;
    tracker.record(first, firstState, 1024);
    tracker.complete(first, 1024);
    tracker.record(second, secondState, 1024);
    tracker.complete(second, 1024);

    expect(tracker.getSpeed()).toBe(2048);
  });
});
