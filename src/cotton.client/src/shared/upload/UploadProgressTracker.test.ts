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
    now = 1000;
    vi.spyOn(Date, "now").mockImplementation(() => now);
  });

  afterEach(() => vi.restoreAllMocks());

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
