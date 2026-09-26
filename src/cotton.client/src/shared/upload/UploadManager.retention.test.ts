import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  settings: vi.fn<
    () => { maxChunkSizeBytes: number; supportedHashAlgorithm: string } | null
  >(() => ({
    maxChunkSizeBytes: 1024,
    supportedHashAlgorithm: "sha256",
  })),
  upload: vi.fn(),
}));

vi.mock("../api/queries/serverSettings", () => ({
  getCachedServerSettings: mocks.settings,
}));
vi.mock("../store/nodesActions", () => ({
  refreshNodeContent: vi.fn(async () => undefined),
}));
vi.mock("./uploadFileToNode", () => ({
  uploadFileToNode: mocks.upload,
}));

import { queryClient } from "../api/queries/queryClient";
import { queryKeys } from "../api/queries/queryKeys";
import { UploadManager } from "./UploadManager";

describe("UploadManager input retention", () => {
  let manager: UploadManager;

  beforeEach(() => {
    queryClient.clear();
    queryClient.setQueryData(queryKeys.storageQuota.current(), {
      usedBytes: 0,
      quotaBytes: null,
      availableBytes: null,
    });
    manager = new UploadManager();
  });

  afterEach(() => {
    manager.destroy();
    queryClient.clear();
    mocks.settings.mockReset();
    mocks.settings.mockReturnValue({
      maxChunkSizeBytes: 1024,
      supportedHashAlgorithm: "sha256",
    });
    mocks.upload.mockReset();
  });

  it("keeps inputs during upload and releases them after completion", async () => {
    let finish!: (file: { id: string; name: string }) => void;
    mocks.upload.mockImplementation(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    const file = new File(["content"], "report.txt");
    const onFileUploaded = vi.fn();

    manager.enqueue([file], "node-1", "Folder", { onFileUploaded });
    await vi.waitFor(() => expect(mocks.upload).toHaveBeenCalledOnce());

    const task = manager["tasks"][0];
    expect(task._file).toBe(file);
    expect(task._onFileUploaded).toBe(onFileUploaded);

    finish({ id: "file-1", name: "report.txt" });
    await vi.waitFor(() => expect(task.status).toBe("completed"));

    expect(onFileUploaded).toHaveBeenCalledOnce();
    expect(task._file).toBeUndefined();
    expect(task._onFileUploaded).toBeUndefined();
    expect(manager.getSnapshot().tasks[0]).toMatchObject({
      status: "completed",
      label: "report.txt",
    });
  });

  it("releases inputs when upload fails", async () => {
    mocks.upload.mockRejectedValue(new Error("network failure"));
    manager.enqueue([new File(["x"], "report.txt")], "node-1", "Folder", {
      onFileUploaded: vi.fn(),
    });

    await vi.waitFor(() => expect(manager["tasks"][0].status).toBe("failed"));

    expect(manager["tasks"][0]._file).toBeUndefined();
    expect(manager["tasks"][0]._onFileUploaded).toBeUndefined();
    expect(manager.getSnapshot().tasks[0].errorKey).toBe("uploadFailed");
  });

  it("releases inputs when server settings are unavailable", () => {
    mocks.settings.mockReturnValue(null);
    manager.enqueue([new File(["x"], "report.txt")], "node-1", "Folder", {
      onFileUploaded: vi.fn(),
    });

    expect(manager["tasks"][0]._file).toBeUndefined();
    expect(manager["tasks"][0]._onFileUploaded).toBeUndefined();
    expect(manager.getSnapshot().tasks[0].errorKey).toBe(
      "serverSettingsNotLoaded",
    );
  });

  it("releases inputs when quota denies an upload", () => {
    queryClient.setQueryData(queryKeys.storageQuota.current(), {
      usedBytes: 1,
      quotaBytes: 1,
      availableBytes: 0,
    });
    manager.enqueue([new File(["x"], "report.txt")], "node-1", "Folder", {
      onFileUploaded: vi.fn(),
    });

    expect(manager["tasks"][0]._file).toBeUndefined();
    expect(manager["tasks"][0]._onFileUploaded).toBeUndefined();
    expect(manager.getSnapshot().tasks[0].errorKey).toBe(
      "storageQuotaExceeded",
    );
    expect(mocks.upload).not.toHaveBeenCalled();
  });
});
