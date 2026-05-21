import { afterEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  getCachedServerSettings: vi.fn(() => ({
    maxChunkSizeBytes: 1024,
    supportedHashAlgorithm: "sha256",
  })),
  getCurrentQuota: vi.fn(async () => ({
    usedBytes: 0,
    quotaBytes: null,
    availableBytes: null,
  })),
  uploadFileToNode: vi.fn(),
}));

vi.mock("../api/queries/serverSettings", () => ({
  getCachedServerSettings: mocks.getCachedServerSettings,
}));

vi.mock("../api/storageQuotaApi", () => ({
  storageQuotaApi: {
    getCurrent: mocks.getCurrentQuota,
  },
}));

vi.mock("./uploadFileToNode", () => ({
  uploadFileToNode: mocks.uploadFileToNode,
}));

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
    mocks.getCachedServerSettings.mockReturnValue({
      maxChunkSizeBytes: 1024,
      supportedHashAlgorithm: "sha256",
    });
    mocks.getCurrentQuota.mockResolvedValue({
      usedBytes: 0,
      quotaBytes: null,
      availableBytes: null,
    });
    mocks.uploadFileToNode.mockReset();
  });

  it("uses visible upload progress for speed when transport bytes are unavailable", async () => {
    const taskManager = createManager();
    let finishUpload!: () => void;
    const uploadFinished = new Promise<void>((resolve) => {
      finishUpload = resolve;
    });

    mocks.uploadFileToNode.mockImplementation(async (options) => {
      options.onProgress?.(512, {
        bytesUploaded: 512,
        bytesConfirmed: 512,
        bytesInFlight: 0,
        bytesTransmitted: 0,
      });
      await uploadFinished;
    });

    taskManager.enqueue(
      [new File([new Uint8Array(1024)], "video.mov")],
      "node-1",
      "Library",
    );

    await waitFor(() =>
      taskManager.getSnapshot().tasks[0]?.speedBytesPerSec !== undefined &&
      taskManager.getSnapshot().tasks[0]!.speedBytesPerSec! > 0,
    );

    expect(taskManager.getSnapshot().tasks[0]).toMatchObject({
      bytesCompleted: 512,
      progress01: 0.5,
      speedBytesPerSec: expect.any(Number),
      status: "running",
    });

    finishUpload();
    await waitFor(() => taskManager.getSnapshot().tasks[0]?.status === "completed");
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

const waitFor = async (predicate: () => boolean): Promise<void> => {
  for (let attempt = 0; attempt < 50; attempt += 1) {
    if (predicate()) {
      return;
    }

    await new Promise((resolve) => setTimeout(resolve, 0));
  }

  throw new Error("Timed out waiting for condition.");
};
