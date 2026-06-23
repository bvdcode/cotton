import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

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
  refreshNodeContent: vi.fn(() => Promise.resolve()),
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

vi.mock("../store/nodesActions", () => ({
  refreshNodeContent: mocks.refreshNodeContent,
}));

vi.mock("./uploadFileToNode", () => ({
  uploadFileToNode: mocks.uploadFileToNode,
}));

import type { NodeContentDto, NodeFileManifestDto } from "../api/nodesApi";
import { queryClient } from "../api/queries/queryClient";
import { queryKeys } from "../api/queries/queryKeys";
import { useNodesStore } from "../store/nodesStore";
import { UploadManager } from "./UploadManager";

let manager: UploadManager | null = null;

const resetNodesStore = () => {
  useNodesStore.setState({
    cacheOwnerUserId: null,
    rootNodeId: null,
    currentNode: null,
    ancestors: [],
    contentByNodeId: {},
    ancestorsByNodeId: {},
    loading: false,
    error: null,
    lastUpdatedByNodeId: {},
  });
};

const makeFileDto = (
  id: string,
  name: string,
  overrides: Partial<NodeFileManifestDto> = {},
): NodeFileManifestDto => ({
  id,
  createdAt: "",
  updatedAt: "",
  nodeId: "node-1",
  ownerId: "user-1",
  name,
  contentType: "text/plain",
  sizeBytes: 0,
  metadata: {},
  ...overrides,
});

const seedParent = (nodeId: string, files: NodeFileManifestDto[]) => {
  const content: NodeContentDto = {
    id: nodeId,
    createdAt: "",
    updatedAt: "",
    nodes: [],
    files,
  };
  useNodesStore.setState((prev) => ({
    ...prev,
    contentByNodeId: {
      ...prev.contentByNodeId,
      [nodeId]: content,
    },
  }));
};

const createManager = () => {
  manager = new UploadManager();
  return manager;
};

describe("UploadManager task facade", () => {
  beforeEach(() => {
    queryClient.clear();
    resetNodesStore();
    sessionStorage.clear();
  });

  afterEach(() => {
    manager?.destroy();
    manager = null;
    queryClient.clear();
    resetNodesStore();
    sessionStorage.clear();
    mocks.getCachedServerSettings.mockReset();
    mocks.getCachedServerSettings.mockReturnValue({
      maxChunkSizeBytes: 1024,
      supportedHashAlgorithm: "sha256",
    });
    mocks.getCurrentQuota.mockReset();
    mocks.getCurrentQuota.mockResolvedValue({
      usedBytes: 0,
      quotaBytes: null,
      availableBytes: null,
    });
    mocks.refreshNodeContent.mockReset();
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

    await waitFor(
      () =>
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
    await waitFor(
      () => taskManager.getSnapshot().tasks[0]?.status === "completed",
    );
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

  it("passes replacement targets to upload finalization", async () => {
    const taskManager = createManager();
    mocks.uploadFileToNode.mockResolvedValue(undefined);

    taskManager.enqueue(
      [
        {
          file: new File(["replacement"], "report.txt"),
          replaceNodeFileId: "file-1",
        },
      ],
      "node-1",
      "Library",
    );

    await waitFor(() => mocks.uploadFileToNode.mock.calls.length > 0);

    expect(mocks.uploadFileToNode).toHaveBeenCalledWith(
      expect.objectContaining({
        file: expect.any(File),
        nodeId: "node-1",
        replaceNodeFileId: "file-1",
      }),
    );
  });

  it("notifies listeners when a file upload is committed", async () => {
    const taskManager = createManager();
    const onFileUploaded = vi.fn();
    const uploadedFile = { id: "file-1", name: "report.txt" };
    mocks.uploadFileToNode.mockResolvedValue(uploadedFile);

    taskManager.enqueue(
      [new File(["replacement"], "report.txt")],
      "node-1",
      "Library",
      { onFileUploaded },
    );

    await waitFor(() => onFileUploaded.mock.calls.length > 0);

    expect(onFileUploaded).toHaveBeenCalledWith(uploadedFile);
  });

  it("uses a fresh cached quota snapshot without fetching before upload", async () => {
    const taskManager = createManager();
    queryClient.setQueryData(queryKeys.storageQuota.current(), {
      usedBytes: 0,
      quotaBytes: 100,
      availableBytes: 100,
    });
    mocks.uploadFileToNode.mockResolvedValue(undefined);

    taskManager.enqueue(
      [new File(["12345"], "paper.txt")],
      "node-1",
      "Library",
    );

    await waitFor(() => mocks.uploadFileToNode.mock.calls.length > 0);

    expect(mocks.getCurrentQuota).not.toHaveBeenCalled();
  });

  it("updates the cached quota snapshot after committed uploads", async () => {
    const taskManager = createManager();
    queryClient.setQueryData(queryKeys.storageQuota.current(), {
      usedBytes: 10,
      quotaBytes: 100,
      availableBytes: 90,
    });
    mocks.uploadFileToNode.mockResolvedValue(undefined);

    taskManager.enqueue(
      [new File(["12345"], "paper.txt")],
      "node-1",
      "Library",
    );

    await waitFor(
      () => taskManager.getSnapshot().tasks[0]?.status === "completed",
    );

    expect(queryClient.getQueryData(queryKeys.storageQuota.current())).toEqual({
      usedBytes: 15,
      quotaBytes: 100,
      availableBytes: 85,
    });
  });

  it("does not refetch quota while draining one upload batch", async () => {
    const dateNow = vi.spyOn(Date, "now");
    let now = 1_000_000;
    dateNow.mockImplementation(() => now);
    const taskManager = createManager();
    let finishFirstUpload!: () => void;
    const firstUploadFinished = new Promise<void>((resolve) => {
      finishFirstUpload = resolve;
    });
    mocks.uploadFileToNode
      .mockImplementationOnce(async () => {
        await firstUploadFinished;
      })
      .mockResolvedValue(undefined);

    try {
      taskManager.enqueue(
        [new File(["a"], "a.txt"), new File(["b"], "b.txt")],
        "node-1",
        "Library",
      );

      await waitFor(() => mocks.uploadFileToNode.mock.calls.length === 1);
      expect(mocks.getCurrentQuota).toHaveBeenCalledTimes(1);

      now += 31 * 60 * 1000;
      finishFirstUpload();

      await waitFor(() => mocks.uploadFileToNode.mock.calls.length === 2);

      expect(mocks.getCurrentQuota).toHaveBeenCalledTimes(1);
    } finally {
      dateNow.mockRestore();
    }
  });

  it("updates cached parent content instead of refreshing after upload", async () => {
    const taskManager = createManager();
    queryClient.setQueryData(queryKeys.storageQuota.current(), {
      usedBytes: 0,
      quotaBytes: null,
      availableBytes: null,
    });
    const uploadedFile = makeFileDto("file-1", "paper.txt");
    seedParent("node-1", []);
    mocks.uploadFileToNode.mockResolvedValue(uploadedFile);

    taskManager.enqueue(
      [new File(["paper"], "paper.txt")],
      "node-1",
      "Library",
    );

    await waitFor(
      () => taskManager.getSnapshot().tasks[0]?.status === "completed",
    );
    await new Promise((resolve) => setTimeout(resolve, 350));

    expect(useNodesStore.getState().contentByNodeId["node-1"]?.files).toEqual([
      uploadedFile,
    ]);
    expect(mocks.refreshNodeContent).not.toHaveBeenCalled();
  });

  it("keeps committed uploads completed when completion listeners fail", async () => {
    const taskManager = createManager();
    const error = new Error("listener failed");
    const onFileUploaded = vi.fn(() => {
      throw error;
    });
    const consoleError = vi
      .spyOn(console, "error")
      .mockImplementation(() => undefined);
    mocks.uploadFileToNode.mockResolvedValue({
      id: "file-1",
      name: "report.txt",
    });

    taskManager.enqueue(
      [new File(["replacement"], "report.txt")],
      "node-1",
      "Library",
      { onFileUploaded },
    );

    await waitFor(
      () => taskManager.getSnapshot().tasks[0]?.status === "completed",
    );

    expect(onFileUploaded).toHaveBeenCalled();
    expect(consoleError).toHaveBeenCalledWith(
      "Upload completion listener failed:",
      error,
    );

    consoleError.mockRestore();
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
